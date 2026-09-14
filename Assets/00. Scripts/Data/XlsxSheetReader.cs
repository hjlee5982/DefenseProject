using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

public static class XlsxSheetReader
{
    private static readonly XNamespace MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelNs = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace DocRelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly Regex CellRefRegex = new(@"^([A-Z]+)(\d+)$", RegexOptions.Compiled);

    public static bool TryReadSheet(
        string xlsxPath,
        string sheetName,
        out List<Dictionary<string, string>> rows,
        out string error)
    {
        rows = null;
        error = null;

        if (string.IsNullOrWhiteSpace(xlsxPath) || !File.Exists(xlsxPath))
        {
            error = $"xlsx not found: {xlsxPath}";
            return false;
        }

        try
        {
            using ZipArchive archive = OpenArchiveShared(xlsxPath);
            List<string> sharedStrings = ReadSharedStrings(archive);
            if (!TryResolveSheetPath(archive, sheetName, out string sheetPath, out error))
                return false;

            ZipArchiveEntry sheetEntry = archive.GetEntry(sheetPath);
            if (sheetEntry == null)
            {
                error = $"sheet entry missing: {sheetPath}";
                return false;
            }

            XDocument sheetDoc;
            using (Stream stream = sheetEntry.Open())
                sheetDoc = XDocument.Load(stream);

            Dictionary<int, Dictionary<int, string>> grid = new();
            int maxCol = -1;
            int maxRow = -1;

            XElement sheetData = sheetDoc.Root?.Element(MainNs + "sheetData");
            if (sheetData == null)
            {
                rows = new List<Dictionary<string, string>>();
                return true;
            }

            foreach (XElement rowElement in sheetData.Elements(MainNs + "row"))
            {
                foreach (XElement cellElement in rowElement.Elements(MainNs + "c"))
                {
                    string cellRef = (string)cellElement.Attribute("r");
                    if (string.IsNullOrEmpty(cellRef) || !TryParseCellRef(cellRef, out int col, out int row))
                        continue;

                    string value = ReadCellValue(cellElement, sharedStrings);
                    if (!grid.TryGetValue(row, out Dictionary<int, string> rowMap))
                    {
                        rowMap = new Dictionary<int, string>();
                        grid[row] = rowMap;
                    }

                    rowMap[col] = value;
                    maxCol = Math.Max(maxCol, col);
                    maxRow = Math.Max(maxRow, row);
                }
            }

            if (maxRow < 0 || maxCol < 0)
            {
                rows = new List<Dictionary<string, string>>();
                return true;
            }

            if (!grid.TryGetValue(0, out Dictionary<int, string> headerRow))
            {
                error = $"sheet '{sheetName}' has no header row";
                return false;
            }

            string[] headers = new string[maxCol + 1];
            for (int col = 0; col <= maxCol; col++)
            {
                headers[col] = headerRow.TryGetValue(col, out string header)
                    ? header.Trim()
                    : string.Empty;
            }

            rows = new List<Dictionary<string, string>>();
            for (int row = 1; row <= maxRow; row++)
            {
                if (!grid.TryGetValue(row, out Dictionary<int, string> values))
                    continue;

                Dictionary<string, string> mapped = new(StringComparer.OrdinalIgnoreCase);
                bool anyValue = false;
                for (int col = 0; col <= maxCol; col++)
                {
                    string header = headers[col];
                    if (string.IsNullOrEmpty(header)) continue;

                    string cellValue = values.TryGetValue(col, out string raw) ? raw : string.Empty;
                    if (!string.IsNullOrWhiteSpace(cellValue))
                        anyValue = true;

                    mapped[header] = cellValue;
                }

                if (anyValue)
                    rows.Add(mapped);
            }

            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            rows = null;
            return false;
        }
    }

    private static ZipArchive OpenArchiveShared(string xlsxPath)
    {
        // Excel may keep the workbook locked while editing; copy under shared read.
        using FileStream fileStream = new(
            xlsxPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        MemoryStream memoryStream = new();
        fileStream.CopyTo(memoryStream);
        memoryStream.Position = 0;
        return new ZipArchive(memoryStream, ZipArchiveMode.Read, leaveOpen: false);
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        List<string> sharedStrings = new();
        ZipArchiveEntry entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry == null) return sharedStrings;

        XDocument doc;
        using (Stream stream = entry.Open())
            doc = XDocument.Load(stream);

        foreach (XElement si in doc.Root.Elements(MainNs + "si"))
        {
            StringBuilder builder = new();
            foreach (XElement text in si.Descendants(MainNs + "t"))
                builder.Append(text.Value);
            sharedStrings.Add(builder.ToString());
        }

        return sharedStrings;
    }

    private static bool TryResolveSheetPath(
        ZipArchive archive,
        string sheetName,
        out string sheetPath,
        out string error)
    {
        sheetPath = null;
        error = null;

        ZipArchiveEntry workbookEntry = archive.GetEntry("xl/workbook.xml");
        ZipArchiveEntry relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
        if (workbookEntry == null || relsEntry == null)
        {
            error = "workbook.xml or workbook.xml.rels missing";
            return false;
        }

        XDocument workbookDoc;
        using (Stream stream = workbookEntry.Open())
            workbookDoc = XDocument.Load(stream);

        XDocument relsDoc;
        using (Stream stream = relsEntry.Open())
            relsDoc = XDocument.Load(stream);

        Dictionary<string, string> relMap = new();
        foreach (XElement rel in relsDoc.Root.Elements(RelNs + "Relationship"))
        {
            string id = (string)rel.Attribute("Id");
            string target = (string)rel.Attribute("Target");
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(target))
                relMap[id] = target.Replace('\\', '/');
        }

        foreach (XElement sheet in workbookDoc.Root.Element(MainNs + "sheets")?.Elements(MainNs + "sheet") ?? Array.Empty<XElement>())
        {
            string name = (string)sheet.Attribute("name");
            if (!string.Equals(name, sheetName, StringComparison.OrdinalIgnoreCase))
                continue;

            string relId = (string)sheet.Attribute(DocRelNs + "id");
            if (string.IsNullOrEmpty(relId) || !relMap.TryGetValue(relId, out string target))
            {
                error = $"relationship missing for sheet '{sheetName}'";
                return false;
            }

            sheetPath = target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
                ? target
                : "xl/" + target.TrimStart('/');
            return true;
        }

        error = $"sheet not found: {sheetName}";
        return false;
    }

    private static string ReadCellValue(XElement cellElement, List<string> sharedStrings)
    {
        string type = (string)cellElement.Attribute("t");
        XElement valueElement = cellElement.Element(MainNs + "v");
        if (valueElement == null) return string.Empty;

        string raw = valueElement.Value;
        if (type == "s" && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sharedIndex))
        {
            if (sharedIndex >= 0 && sharedIndex < sharedStrings.Count)
                return sharedStrings[sharedIndex];
            return string.Empty;
        }

        if (type == "inlineStr")
        {
            XElement text = cellElement.Element(MainNs + "is")?.Element(MainNs + "t");
            return text?.Value ?? string.Empty;
        }

        return raw;
    }

    private static bool TryParseCellRef(string cellRef, out int col, out int row)
    {
        col = 0;
        row = 0;
        Match match = CellRefRegex.Match(cellRef);
        if (!match.Success) return false;

        string colText = match.Groups[1].Value;
        for (int i = 0; i < colText.Length; i++)
            col = col * 26 + (colText[i] - 'A' + 1);
        col -= 1;

        if (!int.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out row))
            return false;

        row -= 1;
        return row >= 0 && col >= 0;
    }
}
