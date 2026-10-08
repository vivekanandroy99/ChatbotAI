using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace ChatbotAI.Dialogue
{
    /// The smallest real Excel file (.xlsx = a zip of XML parts): several sheets of text and numbers, a bold first row.
    /// Opens in Excel, LibreOffice and Google Sheets without warnings; Hindi text is fine (UTF-8).
    public static class SimpleXlsx
    {
        public class Sheet
        {
            public string name;
            public List<object[]> rows = new List<object[]>();
        }

        public static void Write(string path, IList<Sheet> sheets)
        {
            using (var zip = new ZipArchive(new FileStream(path, FileMode.Create), ZipArchiveMode.Create))
            {
                Part(zip, "[Content_Types].xml", ContentTypes(sheets.Count));
                Part(zip, "_rels/.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                    "</Relationships>");
                var wb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
                    "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
                var rels = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
                for (int i = 0; i < sheets.Count; i++)
                {
                    string name = SheetName(sheets[i].name, i);
                    wb.Append($"<sheet name=\"{Escape(name)}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
                    rels.Append($"<Relationship Id=\"rId{i + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>");
                    Part(zip, $"xl/worksheets/sheet{i + 1}.xml", SheetXml(sheets[i]));
                }
                rels.Append($"<Relationship Id=\"rId{sheets.Count + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
                wb.Append("</sheets></workbook>");
                rels.Append("</Relationships>");
                Part(zip, "xl/workbook.xml", wb.ToString());
                Part(zip, "xl/_rels/workbook.xml.rels", rels.ToString());
                // Style 1 = bold (the header row).
                Part(zip, "xl/styles.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                    "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
                    "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>" +
                    "<borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf/></cellStyleXfs>" +
                    "<cellXfs count=\"2\"><xf fontId=\"0\"/><xf fontId=\"1\" applyFont=\"1\"/></cellXfs></styleSheet>");
            }
        }

        static string SheetXml(Sheet sheet)
        {
            var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            // Columns a little wider than Excel's default; long text columns wider still.
            int columns = 0;
            foreach (var r in sheet.rows) if (r.Length > columns) columns = r.Length;
            if (columns > 0)
            {
                sb.Append("<cols>");
                for (int c = 0; c < columns; c++)
                {
                    int longest = 8;
                    foreach (var r in sheet.rows)
                        if (c < r.Length && r[c] != null) longest = System.Math.Max(longest, System.Math.Min(80, r[c].ToString().Length));
                    sb.Append($"<col min=\"{c + 1}\" max=\"{c + 1}\" width=\"{longest + 2}\" customWidth=\"1\"/>");
                }
                sb.Append("</cols>");
            }
            sb.Append("<sheetData>");
            for (int r = 0; r < sheet.rows.Count; r++)
            {
                sb.Append($"<row r=\"{r + 1}\">");
                var row = sheet.rows[r];
                for (int c = 0; c < row.Length; c++)
                {
                    string cell = Column(c) + (r + 1);
                    string style = r == 0 ? " s=\"1\"" : "";
                    switch (row[c])
                    {
                        case null:
                            break;
                        case int n:
                            sb.Append($"<c r=\"{cell}\"{style}><v>{n}</v></c>");
                            break;
                        case float f:
                            sb.Append($"<c r=\"{cell}\"{style}><v>{f.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}</v></c>");
                            break;
                        default:
                            sb.Append($"<c r=\"{cell}\" t=\"inlineStr\"{style}><is><t xml:space=\"preserve\">{Escape(row[c].ToString())}</t></is></c>");
                            break;
                    }
                }
                sb.Append("</row>");
            }
            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        static string ContentTypes(int sheets)
        {
            var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            for (int i = 1; i <= sheets; i++)
                sb.Append($"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            sb.Append("</Types>");
            return sb.ToString();
        }

        static void Part(ZipArchive zip, string name, string xml)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (var w = new StreamWriter(entry.Open(), new UTF8Encoding(false))) w.Write(xml);
        }

        static string Column(int index)
        {
            string s = "";
            for (index++; index > 0; index = (index - 1) / 26) s = (char)('A' + (index - 1) % 26) + s;
            return s;
        }

        // Sheet names: max 31 characters, none of []:*?/\ .
        static string SheetName(string name, int i)
        {
            var sb = new StringBuilder();
            foreach (char ch in name ?? "") if ("[]:*?/\\".IndexOf(ch) < 0) sb.Append(ch);
            string s = sb.ToString().Trim();
            if (s.Length == 0) s = "Sheet" + (i + 1);
            return s.Length > 31 ? s.Substring(0, 31) : s;
        }

        // XML-escaped, without characters XML can't hold (control characters).
        static string Escape(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s)
                if (ch == '\t' || ch == '\n' || ch == '\r' || ch >= 0x20) sb.Append(ch);
            return SecurityElement.Escape(sb.ToString());
        }
    }
}
