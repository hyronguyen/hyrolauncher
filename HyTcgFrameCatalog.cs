using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace HYROLauncherWeb
{
    public sealed class HyTcgFrameMaterial
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Count { get; set; }
    }
    public sealed class HyTcgFrameLayout
    {
        public double ArtworkX { get; set; } public double ArtworkY { get; set; }
        public double ArtworkWidth { get; set; } public double ArtworkHeight { get; set; }
        public double NameX { get; set; } public double NameY { get; set; }
        public double NameWidth { get; set; } public double NameHeight { get; set; }
        public double SeriesX { get; set; } public double SeriesY { get; set; }
        public double SeriesWidth { get; set; } public double SeriesHeight { get; set; }
        public double RarityX { get; set; } public double RarityY { get; set; }
        public double RarityWidth { get; set; } public double RarityHeight { get; set; }
        public double NumberX { get; set; } public double NumberY { get; set; }
        public double NumberWidth { get; set; } public double NumberHeight { get; set; }
        public double DescriptionX { get; set; } public double DescriptionY { get; set; }
        public double DescriptionWidth { get; set; } public double DescriptionHeight { get; set; }
    }
    public sealed class HyTcgFrameDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Design { get; set; }
        public int PriceGems { get; set; }
        public int GemPrice { get { return PriceGems; } }
        public List<HyTcgFrameMaterial> Materials { get; set; }
        public HyTcgFrameLayout Portrait { get; set; }
        public HyTcgFrameLayout Landscape { get; set; }
        public string PortraitOverlay { get; set; }
        public string LandscapeOverlay { get; set; }
        public string CardDescription { get; set; }
    }

    // Local resources describe appearance only. Native prices and material costs do
    // not come from the renderer or editable catalog, including extension frames.
    internal static class HyTcgFrameCatalog
    {
        private static readonly object gate=new object();
        private static List<HyTcgFrameDefinition> catalog;
        private static readonly string[] legacy={"classic","petal","arcane","circuit","royal"};
        private static readonly string[] designs={"classic","petal","arcane","circuit","royal","moonlace","starlit","sakura","obsidian","gilded"};
        internal static bool Legacy(string id) { return legacy.Contains(id); }
        internal static bool DesignKnown(string id) {return designs.Contains(id);}
        internal static bool OverlayValid(string file) {return ValidOverlay(file);}
        internal static List<HyTcgFrameMaterial> CreationMaterials()
        {return new List<HyTcgFrameMaterial> {Material("fiber",10),Material("petal",5),Material("crystal",5)};}
        internal static bool LayoutValid(HyTcgFrameLayout layout) {return ValidLayout(layout);}
        internal static List<HyTcgFrameDefinition> All()
        {
            lock(gate)
            {
                if(catalog==null)catalog=LoadLocal(ResourceDirectory());
                return Copy(catalog);
            }
        }
        internal static HyTcgFrameDefinition Find(string id)
        { return All().FirstOrDefault(f=>f.Id==id); }
        internal static List<HyTcgFrameDefinition> LoadLocal(string directory)
        {
            var frames=BuiltIn();string file;
            try
            {
                file=SafeResource(directory,"catalog.json");if(!File.Exists(file))return frames;
                byte[] bytes=ReadBounded(file,256*1024);
                var additions=new JavaScriptSerializer {MaxJsonLength=256*1024}.Deserialize<List<HyTcgFrameDefinition>>(Encoding.UTF8.GetString(bytes));
                if(additions==null || additions.Count>54)return frames;
                foreach(var item in additions)
                {
                    if(!Valid(item))continue;
                    var previous=frames.FirstOrDefault(f=>f.Id==item.Id);
                    if(previous!=null)
                    {
                        // An artist may replace a built-in layout or overlay while
                        // keeping the native creation price and recipe unchanged.
                        item.PriceGems=HyTcgEconomy.FrameCreationPrice;item.Materials=CreationMaterials();
                        frames.Remove(previous);
                    }
                    else
                    {
                        item.PriceGems=HyTcgEconomy.FrameCreationPrice;
                        item.Materials=CreationMaterials();
                    }
                    frames.Add(item);
                }
            }
            catch { } // Invalid optional artist resources fall back to built-in frames.
            return frames;
        }
        internal static byte[] ReadOverlay(string resource)
        {
            if(String.IsNullOrEmpty(resource))return null;
            try
            {
                byte[] bytes=ReadBounded(SafeResource(ResourceDirectory(),resource),4*1024*1024);
                if(bytes.Length<24 || !bytes.Take(8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))return null;
                int width,height;byte[] png=HyTcgEconomy.NormalizeCustomArtwork(bytes,out width,out height,false);
                return png;
            }
            catch {return null;}
        }
        internal static string ResourceDirectory()
        { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","hytcg","frames"); }
        private static string SafeResource(string directory,string resource)
        {
            if(!Regex.IsMatch(resource??"",@"^[A-Za-z0-9_-]+\.(png|json)$",RegexOptions.IgnoreCase))throw new HyTcgException("FrameInvalid");
            string root=Path.GetFullPath(directory),file=Path.GetFullPath(Path.Combine(root,resource));
            for(var parent=new DirectoryInfo(root);parent!=null;parent=parent.Parent)
                if(parent.Exists && (parent.Attributes&FileAttributes.ReparsePoint)!=0)throw new HyTcgException("FrameInvalid");
            if(!file.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)
                || (File.Exists(file) && (File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0))throw new HyTcgException("FrameInvalid");
            return file;
        }
        private static byte[] ReadBounded(string file,int maximum)
        {
            using(var input=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read))using(var output=new MemoryStream())
            {
                if(input.Length<1 || input.Length>maximum)throw new HyTcgException("FrameInvalid");
                byte[] buffer=new byte[8192];int count;
                while((count=input.Read(buffer,0,buffer.Length))>0)
                {if(output.Length+count>maximum)throw new HyTcgException("FrameInvalid");output.Write(buffer,0,count);}
                return output.ToArray();
            }
        }
        internal static bool Valid(HyTcgFrameDefinition frame)
        {
            return frame!=null && Regex.IsMatch(frame.Id??"",@"^[a-z][a-z0-9_-]{1,31}$")
                && !String.IsNullOrWhiteSpace(frame.Name) && frame.Name.Length<=64 && !frame.Name.Any(Char.IsControl)
                && frame.Description!=null && frame.Description.Length<=240 && !frame.Description.Any(Char.IsControl)
                && (frame.CardDescription==null || (frame.CardDescription.Length<=240 && !frame.CardDescription.Any(c=>Char.IsControl(c) && c!='\r' && c!='\n' && c!='\t')))
                && designs.Contains(frame.Design) && ValidLayout(frame.Portrait) && ValidLayout(frame.Landscape)
                && ValidOverlay(frame.PortraitOverlay) && ValidOverlay(frame.LandscapeOverlay);
        }
        private static bool ValidOverlay(string file)
        {return file==null || file=="" || Regex.IsMatch(file,@"^[A-Za-z0-9_-]+\.png$",RegexOptions.IgnoreCase);}
        private static bool Box(double x,double y,double width,double height)
        {return !Double.IsNaN(x+y+width+height) && !Double.IsInfinity(x+y+width+height) && x>=0 && y>=0 && width>=0.01 && height>=0.01 && x+width<=1 && y+height<=1;}
        private static bool ValidLayout(HyTcgFrameLayout l)
        {return l!=null && Box(l.ArtworkX,l.ArtworkY,l.ArtworkWidth,l.ArtworkHeight) && Box(l.NameX,l.NameY,l.NameWidth,l.NameHeight)
            && Box(l.SeriesX,l.SeriesY,l.SeriesWidth,l.SeriesHeight) && Box(l.RarityX,l.RarityY,l.RarityWidth,l.RarityHeight)
            && Box(l.NumberX,l.NumberY,l.NumberWidth,l.NumberHeight)
            && ((l.DescriptionWidth==0 && l.DescriptionHeight==0 && l.DescriptionX==0 && l.DescriptionY==0)
                || Box(l.DescriptionX,l.DescriptionY,l.DescriptionWidth,l.DescriptionHeight));}
        private static List<HyTcgFrameDefinition> Copy(List<HyTcgFrameDefinition> value)
        {var json=new JavaScriptSerializer();return json.Deserialize<List<HyTcgFrameDefinition>>(json.Serialize(value));}
        private static HyTcgFrameMaterial Material(string id,int count)
        {return new HyTcgFrameMaterial {Id=id,Name=id=="fiber"?"Frame fiber":id=="petal"?"Sakura petals":"Arcane crystal",Count=count};}
        private static HyTcgFrameLayout Layout(bool landscape,bool template)
        {
            if(!template)return new HyTcgFrameLayout {ArtworkX=0.025,ArtworkY=0.025,ArtworkWidth=0.95,ArtworkHeight=0.95,
                NameX=0.067,NameY=landscape?0.70:0.78,NameWidth=0.84,NameHeight=0.10,
                SeriesX=0.067,SeriesY=landscape?0.82:0.865,SeriesWidth=0.84,SeriesHeight=0.05,
                RarityX=0.76,RarityY=0.055,RarityWidth=0.17,RarityHeight=0.05,
                NumberX=0.067,NumberY=0.943,NumberWidth=0.87,NumberHeight=0.024};
            return landscape?new HyTcgFrameLayout {ArtworkX=0.055,ArtworkY=0.105,ArtworkWidth=0.58,ArtworkHeight=0.79,
                NameX=0.675,NameY=0.29,NameWidth=0.275,NameHeight=0.20,SeriesX=0.675,SeriesY=0.535,SeriesWidth=0.275,SeriesHeight=0.15,
                RarityX=0.73,RarityY=0.14,RarityWidth=0.20,RarityHeight=0.075,NumberX=0.67,NumberY=0.855,NumberWidth=0.29,NumberHeight=0.07}:
                new HyTcgFrameLayout {ArtworkX=0.075,ArtworkY=0.09,ArtworkWidth=0.85,ArtworkHeight=0.60,
                NameX=0.10,NameY=0.735,NameWidth=0.80,NameHeight=0.10,SeriesX=0.10,SeriesY=0.835,SeriesWidth=0.80,SeriesHeight=0.055,
                RarityX=0.70,RarityY=0.022,RarityWidth=0.22,RarityHeight=0.048,NumberX=0.08,NumberY=0.924,NumberWidth=0.84,NumberHeight=0.045};
        }
        private static HyTcgFrameDefinition Definition(string id,string name,int gems,string first,int firstCount,string second,int secondCount)
        {
            var material=new List<HyTcgFrameMaterial>();if(firstCount>0)material.Add(Material(first,firstCount));if(secondCount>0)material.Add(Material(second,secondCount));
            HyTcgFrameLayout portrait=Layout(false,!Legacy(id)),landscape=Layout(true,!Legacy(id));
            if(id=="moonlace") {portrait.ArtworkY=0.075;portrait.ArtworkHeight=0.635;portrait.NameY=0.755;portrait.SeriesY=0.86;landscape.ArtworkX=0.075;landscape.ArtworkWidth=0.535;}
            if(id=="starlit") {portrait.ArtworkX=0.045;portrait.ArtworkWidth=0.91;portrait.ArtworkY=0.065;portrait.ArtworkHeight=0.65;portrait.NameY=0.76;portrait.SeriesY=0.865;landscape.ArtworkX=0.035;landscape.ArtworkWidth=0.61;}
            if(id=="sakura") {portrait.ArtworkX=0.085;portrait.ArtworkWidth=0.83;portrait.NameX=0.135;portrait.NameWidth=0.73;portrait.SeriesX=0.135;portrait.SeriesWidth=0.73;landscape.NameX=0.69;landscape.NameWidth=0.255;}
            if(id=="obsidian") {portrait.ArtworkX=0.095;portrait.ArtworkWidth=0.81;portrait.ArtworkY=0.11;portrait.ArtworkHeight=0.57;portrait.NameY=0.72;portrait.NameHeight=0.115;landscape.ArtworkY=0.135;landscape.ArtworkHeight=0.735;}
            if(id=="gilded") {portrait.ArtworkX=0.12;portrait.ArtworkWidth=0.76;portrait.ArtworkY=0.105;portrait.ArtworkHeight=0.56;portrait.NameY=0.705;portrait.NameHeight=0.135;portrait.SeriesY=0.85;landscape.ArtworkWidth=0.54;landscape.NameX=0.64;landscape.NameWidth=0.30;}
            return new HyTcgFrameDefinition {Id=id,Name=name,Description=Legacy(id)?"Original display frame.":"Designed frame with a dedicated artwork window and identity layout.",Design=id,PriceGems=HyTcgEconomy.FrameCreationPrice,
                Materials=CreationMaterials(),Portrait=portrait,Landscape=landscape,PortraitOverlay="",LandscapeOverlay="",CardDescription=""};
        }
        private static List<HyTcgFrameDefinition> BuiltIn()
        {
            return new List<HyTcgFrameDefinition> {
                Definition("classic","Classic",0,"",0,"",0),Definition("petal","Petal",80,"fiber",1,"",0),Definition("arcane","Arcane",120,"crystal",1,"",0),
                Definition("circuit","Circuit",100,"fiber",1,"",0),Definition("royal","Royal",160,"petal",1,"crystal",1),
                Definition("moonlace","Moonlace",180,"fiber",4,"petal",2),Definition("starlit","Starlit",220,"fiber",2,"crystal",3),
                Definition("sakura","Sakura Scroll",200,"petal",4,"fiber",2),Definition("obsidian","Obsidian Gate",260,"crystal",4,"fiber",2),
                Definition("gilded","Gilded Crest",320,"crystal",4,"petal",4)};
        }
    }
}
