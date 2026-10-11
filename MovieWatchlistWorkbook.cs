using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace HYROLauncherWeb
{
    // Small native OpenXML codec: no Excel installation or runtime package needed.
    public static class MovieWatchlistWorkbook
    {
        private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly string[] Headers = { "Id", "Title", "Episode", "TotalEpisodes", "Status", "WatchTime", "Url", "PosterPath", "UpdatedAt" };
        public static void Export(string path, List<MovieWatchItem> items)
        {
            using (FileStream stream = File.Create(path))
            using (ZipArchive zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                Write(zip,"[Content_Types].xml","<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/></Types>");
                Write(zip,"_rels/.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
                Write(zip,"xl/workbook.xml","<workbook xmlns=\""+Ns+"\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Movie Watchlist\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
                Write(zip,"xl/_rels/workbook.xml.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
                Write(zip,"xl/styles.xml","<styleSheet xmlns=\""+Ns+"\"><fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><color rgb=\"FFFFFFFF\"/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts><fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF182C44\"/><bgColor indexed=\"64\"/></patternFill></fill></fills><borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs><cellXfs count=\"2\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFill=\"1\" applyFont=\"1\"/></cellXfs></styleSheet>");
                XElement data = new XElement(Ns+"sheetData", Row(1,Headers,true));
                int row=2;
                foreach(MovieWatchItem item in items ?? new List<MovieWatchItem>()) data.Add(Row(row++,new [] { item.Id,item.Title,item.Episode,item.TotalEpisodes.ToString(CultureInfo.InvariantCulture),item.Status,item.WatchTime,item.Url,item.PosterPath,item.UpdatedAt },false));
                XElement sheet = new XElement(Ns+"worksheet",
                    new XElement(Ns+"sheetViews",new XElement(Ns+"sheetView",new XAttribute("workbookViewId",0),new XElement(Ns+"pane",new XAttribute("ySplit",1),new XAttribute("topLeftCell","A2"),new XAttribute("activePane","bottomLeft"),new XAttribute("state","frozen")))),
                    new XElement(Ns+"cols",Enumerable.Range(1,9).Select(i=>new XElement(Ns+"col",new XAttribute("min",i),new XAttribute("max",i),new XAttribute("width",i==2?34:i==7||i==8?55:22),new XAttribute("customWidth",1)))),
                    data,new XElement(Ns+"autoFilter",new XAttribute("ref","A1:I"+Math.Max(1,row-1))));
                Write(zip,"xl/worksheets/sheet1.xml",sheet.ToString());
            }
        }
        private static XElement Row(int number,string[] cells,bool header)
        {
            return new XElement(Ns+"row",new XAttribute("r",number),cells.Select((value,i)=>new XElement(Ns+"c",new XAttribute("r",((char)('A'+i)).ToString()+number),new XAttribute("t","inlineStr"),new XAttribute("s",header?1:0),new XElement(Ns+"is",new XElement(Ns+"t",new XAttribute(XNamespace.Xml+"space","preserve"),SafeText(value))))));
        }
        private static string SafeText(string value)
        {
            var result=new System.Text.StringBuilder(); value=value??"";
            for(int i=0;i<value.Length;i++)
            {
                if(XmlConvert.IsXmlChar(value[i])) result.Append(value[i]);
                else if(Char.IsHighSurrogate(value[i])&&i+1<value.Length&&Char.IsLowSurrogate(value[i+1])) { result.Append(value[i]); result.Append(value[++i]); }
            }
            return result.ToString();
        }
        private static void Write(ZipArchive zip,string path,string value)
        {
            if(path=="xl/styles.xml") value=value.Replace("</styleSheet>","<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>");
            using(StreamWriter writer=new StreamWriter(zip.CreateEntry(path).Open(),new System.Text.UTF8Encoding(false))) writer.Write(value);
        }
        private static XDocument Read(ZipArchiveEntry entry)
        {
            if(entry==null || entry.Length>20*1024*1024) throw new InvalidDataException("Workbook content missing or too large.");
            using(Stream stream=entry.Open()) using(XmlReader reader=XmlReader.Create(stream,new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=20*1024*1024 })) return XDocument.Load(reader);
        }
        public static List<MovieWatchItem> Import(string path)
        {
            if(new FileInfo(path).Length>20*1024*1024) throw new InvalidDataException("Workbook must be smaller than 20 MB.");
            using(ZipArchive zip=ZipFile.OpenRead(path))
            {
                var sharedEntry=zip.GetEntry("xl/sharedStrings.xml");
                string[] shared=sharedEntry==null?new string[0]:Read(sharedEntry).Descendants(Ns+"si").Select(si=>String.Concat(si.Descendants(Ns+"t").Select(t=>t.Value))).ToArray();
                var workbook=Read(zip.GetEntry("xl/workbook.xml"));
                var first=workbook.Descendants(Ns+"sheet").FirstOrDefault();
                if(first==null) throw new InvalidDataException("No worksheet found.");
                string relId=(string)first.Attribute(XName.Get("id","http://schemas.openxmlformats.org/officeDocument/2006/relationships"));
                var relationships=Read(zip.GetEntry("xl/_rels/workbook.xml.rels"));
                var relationship=relationships.Root.Elements().FirstOrDefault(e=>(string)e.Attribute("Id")==relId);
                string target=relationship==null?"":(string)relationship.Attribute("Target");
                if(String.IsNullOrEmpty(target) || target.Contains("..") || (string)relationship.Attribute("TargetMode")=="External") throw new InvalidDataException("Invalid worksheet path.");
                string sheetPath=target.StartsWith("/")?target.TrimStart('/') : "xl/"+target;
                var rows=Read(zip.GetEntry(sheetPath)).Descendants(Ns+"row").Take(10002).ToList();
                if(rows.Count>10001) throw new InvalidDataException("At most 10000 movies can be imported.");
                if(rows.Count==0) return new List<MovieWatchItem>();
                var headers=Cells(rows[0],shared).ToDictionary(x=>x.Key,x=>x.Value.Trim(),StringComparer.OrdinalIgnoreCase);
                if(!headers.Values.Contains("Title",StringComparer.OrdinalIgnoreCase)) throw new InvalidDataException("Missing Title column. Use a HYRO Movie Watchlist export.");
                var result=new List<MovieWatchItem>();
                foreach(var row in rows.Skip(1))
                {
                    var cells=Cells(row,shared); var fields=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                    foreach(var pair in headers) { string value; fields[pair.Value]=cells.TryGetValue(pair.Key,out value)?value:""; }
                    Func<string,string> get=key=>fields.ContainsKey(key)?fields[key].Trim():"";
                    if(String.IsNullOrWhiteSpace(get("Title"))) continue;
                    int total; Int32.TryParse(get("TotalEpisodes"),out total);
                    result.Add(new MovieWatchItem { Id=get("Id"),Title=get("Title"),Episode=get("Episode"),TotalEpisodes=Math.Max(0,Math.Min(10000,total)),Status=get("Status"),WatchTime=get("WatchTime"),Url=get("Url"),PosterPath=get("PosterPath"),UpdatedAt=get("UpdatedAt"),SourceGameId="" });
                }
                return result;
            }
        }
        private static Dictionary<string,string> Cells(XElement row,string[] shared)
        {
            var result=new Dictionary<string,string>();
            foreach(var cell in row.Elements(Ns+"c"))
            {
                string reference=(string)cell.Attribute("r")??"", column=new string(reference.TakeWhile(Char.IsLetter).ToArray());
                string type=(string)cell.Attribute("t"), value=(string)cell.Element(Ns+"v")??"";
                int index;
                if(type=="inlineStr") value=String.Concat(cell.Descendants(Ns+"t").Select(t=>t.Value));
                else if(type=="s") value=Int32.TryParse(value,out index)&&index>=0&&index<shared.Length?shared[index]:"";
                result[column]=value;
            }
            return result;
        }
    }
}
