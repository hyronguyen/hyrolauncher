using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
class RarityRimVerification
{
 static Assembly app;static Type cardType,styleType,rendererType,frameType;static int checks;
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
 static void Set(object target,string name,object value){target.GetType().GetProperty(name).SetValue(target,value,null);}
 static string Data(string path){return "data:image/png;base64,"+Convert.ToBase64String(File.ReadAllBytes(path));}
 static Bitmap Pixels(string value){using(var input=new MemoryStream(Convert.FromBase64String(value.Substring(value.IndexOf(',')+1))))using(var image=Image.FromStream(input)){return new Bitmap(image);}}
 static object Card(string rarity,string orientation,string marker){var card=Activator.CreateInstance(cardType);Set(card,"CardId","HT-20261011-ABCDEF123456");Set(card,"CharacterId",99991);Set(card,"Name","Meredy");Set(card,"Series","Tales of Eternia");Set(card,"Gender","Female");Set(card,"Rarity",rarity);Set(card,"RarityColor","#76b8b7");Set(card,"Orientation",orientation);Set(card,"ImageSource","zerochan");Set(card,"Metadata",new Dictionary<string,string>{{"hytcg_card_layout",marker},{"desc","A travelling companion from Tales of Eternia."}});return card;}
 static object Style(string frame,string dye,bool mystic){var style=Activator.CreateInstance(styleType);Set(style,"CardId","HT-20261011-ABCDEF123456");Set(style,"Frame",frame);Set(style,"FrameItemId",frame=="classic"?"":"retired-inventory-frame");Set(style,"Dye",dye);Set(style,"Morph","");Set(style,"MorphMode","none");Set(style,"Mystic",mystic);return style;}
 static string Render(object card,string canonical,object style,string source,object definition,string overlay,bool original){return (string)rendererType.GetMethod("Render",BindingFlags.NonPublic|BindingFlags.Static,null,new[]{cardType,typeof(string),styleType,typeof(string),typeof(bool),frameType,typeof(string),typeof(bool)},null).Invoke(null,new[]{card,canonical,style,source,false,definition,overlay,original});}
 static void Equal(Bitmap first,Bitmap second,Rectangle box,string label){for(int y=box.Top;y<box.Bottom;y++)for(int x=box.Left;x<box.Right;x++)if(first.GetPixel(x,y).ToArgb()!=second.GetPixel(x,y).ToArgb())throw new Exception(label+" changed at "+x+","+y);checks++;}
 static void InteriorEqual(Bitmap first,Bitmap second,string label){for(int y=34;y<first.Height-34;y++)for(int x=34;x<first.Width-34;x++){float nearestX=Math.Max(47,Math.Min(first.Width-47,x)),nearestY=Math.Max(47,Math.Min(first.Height-47,y));if((x-nearestX)*(x-nearestX)+(y-nearestY)*(y-nearestY)>13*13)continue;if(first.GetPixel(x,y).ToArgb()!=second.GetPixel(x,y).ToArgb())throw new Exception(label+" changed at "+x+","+y);}checks++;}
 static string RimHash(Bitmap pixels){using(var output=new MemoryStream())using(var writer=new BinaryWriter(output))using(var sha=SHA256.Create()){for(int y=0;y<pixels.Height;y++)for(int x=0;x<pixels.Width;x++)if(x<31||y<31||x>=pixels.Width-31||y>=pixels.Height-31)writer.Write(pixels.GetPixel(x,y).ToArgb());return Convert.ToBase64String(sha.ComputeHash(output.ToArray()));}}
 static int ChangedRim(Bitmap first,Bitmap second){int changed=0;for(int y=0;y<first.Height;y++)for(int x=0;x<first.Width;x++)if((x<31||y<31||x>=first.Width-31||y>=first.Height-31)&&first.GetPixel(x,y).ToArgb()!=second.GetPixel(x,y).ToArgb())changed++;return changed;}
 static int Exact(Bitmap image,Color color){int n=0;for(int y=34;y<image.Height-34;y++)for(int x=34;x<image.Width-34;x++)if(image.GetPixel(x,y).ToArgb()==color.ToArgb())n++;return n;}
 static void Main(string[] args)
 {
  app=Assembly.LoadFrom(args[0]);cardType=app.GetType("HYROLauncherWeb.HyTcgCard");styleType=app.GetType("HYROLauncherWeb.HyTcgCosmetic");rendererType=app.GetType("HYROLauncherWeb.HyTcgCosmeticRenderer");frameType=app.GetType("HYROLauncherWeb.HyTcgFrameDefinition");Directory.CreateDirectory(args[2]);var rimHashes=new HashSet<string>();
  foreach(string rarity in new[]{"Common","Uncommon","Rare","SR","SSR","UR"})foreach(string orientation in new[]{"portrait","landscape"})
  {
   bool low=rarity!="SSR"&&rarity!="UR";var card=Card(rarity,orientation,low?"window-v3":"full-v3");string canonical=Data(Path.Combine(args[1],rarity+"-"+orientation+".png")),original=Data(Path.Combine(args[1],"source-"+orientation+".png"));
   string regular=Render(card,canonical,Style("classic","#D73658",false),null,null,null,false),mystic=Render(card,canonical,Style("classic","#D73658",true),null,null,null,false);
   using(var before=Pixels(canonical))using(var after=Pixels(regular))using(var glowing=Pixels(mystic))
   {
    Check(before.Size==after.Size,"dimensions "+rarity+orientation);InteriorEqual(before,after,"dye strictly rim "+rarity+orientation);InteriorEqual(before,glowing,"Mystic strictly rim "+rarity+orientation);
    Check(ChangedRim(before,after)>10000,"dye visibly changes wide rim "+rarity+orientation);Check(ChangedRim(after,glowing)>100,"Mystic rim only glow "+rarity+orientation);
    var sample=after.GetPixel(16,(int)(before.Height*.22));Check(sample.R>sample.G*1.3&&sample.R>sample.B,"dye saturated on actual edge "+rarity+orientation);
    if(orientation=="portrait")Check(rimHashes.Add(RimHash(after)),"distinct same-dye tier engravings "+rarity);
    after.Save(Path.Combine(args[2],rarity+"-"+orientation+"-dye.png"));glowing.Save(Path.Combine(args[2],rarity+"-"+orientation+"-mystic.png"));
   }
   string retired=Render(card,canonical,Style("moonlace","#D73658",false),null,Activator.CreateInstance(frameType),"not-a-valid-overlay",false);
   Check(retired==regular,"retired frame and imported overlay cannot replace rarity geometry "+rarity+orientation);
   using(var changed=Pixels(Render(card,canonical,Style("classic","#D73658",false),original,null,null,true)))
   {Check(changed.Width==(orientation=="portrait"?750:1050),"replacement remains correct orientation "+rarity+orientation);if(low){Check(Exact(changed,Color.Red)>100,"replacement full head "+rarity+orientation);Check(Exact(changed,Color.Lime)>100,"replacement full feet "+rarity+orientation);}changed.Save(Path.Combine(args[2],rarity+"-"+orientation+"-original.png"));}
   if(low)
   {
    Set(card,"Metadata",new Dictionary<string,string>{{"hytcg_card_layout","window-v2"},{"desc","A travelling companion from Tales of Eternia."}});
    using(var repaired=Pixels(Render(card,canonical,null,null,null,null,false))){Check(Exact(repaired,Color.Red)>100,"legacy v2 local head "+rarity+orientation);Check(Exact(repaired,Color.Lime)>100,"legacy v2 local feet "+rarity+orientation);}
   }
  }
  Check(!(bool)rendererType.GetMethod("NeedsDefaultRecomposition",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new[]{Card("Common","portrait","window-v3")}),"low v3 no recovery needed");Check(!(bool)rendererType.GetMethod("NeedsDefaultRecomposition",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new[]{Card("UR","portrait","full-v3")}),"high v3 no recovery needed");Check((bool)rendererType.GetMethod("NeedsDefaultRecomposition",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new[]{Card("Common","portrait","window-v2")}),"low v2 recomposes locally");
  File.WriteAllText(Path.Combine(args[2],"report.json"),new JavaScriptSerializer().Serialize(new{Passed=true,Checks=checks}));Console.WriteLine("PASS native rarity rim: "+checks+" checks");
 }
}
