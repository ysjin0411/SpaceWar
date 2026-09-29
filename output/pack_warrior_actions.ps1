$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$dummy = [System.Drawing.Bitmap]::new(1, 1)
$drawingRefs = [AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -match '^(System.Drawing|System.Private.Windows)' } | ForEach-Object Location
Add-Type -ReferencedAssemblies $drawingRefs -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.IO;
public static class ActionFrames {
    // Measure visible pixels inside a supplied body or frame region.
    public static Rectangle Bounds(Bitmap b, Rectangle area, int threshold=24) {
        area.Intersect(new Rectangle(0,0,b.Width,b.Height));
        int l=b.Width,t=b.Height,r=-1,d=-1;
        for(int y=area.Top;y<area.Bottom;y++) for(int x=area.Left;x<area.Right;x++) {
            if(b.GetPixel(x,y).A<threshold) continue;
            l=Math.Min(l,x); t=Math.Min(t,y); r=Math.Max(r,x); d=Math.Max(d,y);
        }
        return r<l ? Rectangle.Empty : Rectangle.FromLTRB(l,t,r+1,d+1);
    }
    // Export an isolated generated frame on a shared canvas without changing its aspect ratio.
    public static string Pack(string input,string output,Rectangle area,Rectangle bodyArea,float sourceAnchorX,float targetHeight,float explicitScale=0) {
        using(var src=new Bitmap(input)) {
            Rectangle all=Bounds(src,area,4), body=Bounds(src,bodyArea,80);
            if(all.IsEmpty || body.IsEmpty) throw new Exception("Empty frame or body: "+output);
            float scale=explicitScale>0 ? explicitScale : targetHeight/body.Height;
            float dx=1024+(all.X-sourceAnchorX)*scale,dy=1162+(all.Y-body.Bottom)*scale;
            if(dx<2 || dy<2 || dx+all.Width*scale>2046 || dy+all.Height*scale>2046) throw new Exception("Canvas clipping: "+output);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            using(var result=new Bitmap(2048,2048,PixelFormat.Format32bppArgb)) {
                using(var g=Graphics.FromImage(result)) {
                    g.Clear(Color.Transparent); g.CompositingMode=CompositingMode.SourceCopy;
                    g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
                    g.DrawImage(src,new RectangleF(dx,dy,all.Width*scale,all.Height*scale),all,GraphicsUnit.Pixel);
                }
                result.Save(output,ImageFormat.Png);
            }
            return Path.GetFileName(output)+","+body.Height+","+scale.ToString(System.Globalization.CultureInfo.InvariantCulture)+","+(body.Height*scale).ToString(System.Globalization.CultureInfo.InvariantCulture)+","+(all.Height*scale).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
    // Normalize generated body and shield independently around their shared hand contact.
    public static string PackShield(string input,string output,Rectangle area,Rectangle bodyArea,int splitX,int handY,float shieldHeight) {
        using(var src=new Bitmap(input)) {
            Rectangle body=Bounds(src,bodyArea,80), all=Bounds(src,area,16);
            float bs=264f/body.Height;
            int maskTop=body.Top-12,maskBottom=body.Bottom+12;
            using(var bodyImage=new Bitmap(src.Width,src.Height,PixelFormat.Format32bppArgb))
            using(var shieldImage=new Bitmap(src.Width,src.Height,PixelFormat.Format32bppArgb)) {
                for(int y=area.Top;y<area.Bottom;y++) for(int x=area.Left;x<area.Right;x++) {
                    Color p=src.GetPixel(x,y);
                    if(p.A<80) p=Color.Transparent;
                    if(x<splitX && y>=maskTop && y<maskBottom) bodyImage.SetPixel(x,y,p);
                    else shieldImage.SetPixel(x,y,p);
                }
                Rectangle br=Bounds(bodyImage,area,16),sr=Bounds(shieldImage,area,16);
                float ss=shieldHeight/sr.Height;
                float anchorX=(br.Left+splitX)*0.5f;
                float contactX=1024+(splitX-anchorX)*bs,contactY=1162+(handY-body.Bottom)*bs;
                RectangleF bd=new RectangleF(1024+(br.X-anchorX)*bs,1162+(br.Y-body.Bottom)*bs,br.Width*bs,br.Height*bs);
                RectangleF sd=new RectangleF(contactX+(sr.X-splitX)*ss,contactY-shieldHeight*0.5f,sr.Width*ss,shieldHeight);
                if(sd.Top<2 || sd.Bottom>2046 || sd.Left<2 || sd.Right>2046) throw new Exception("Shield outside canvas: "+output);
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                using(var result=new Bitmap(2048,2048,PixelFormat.Format32bppArgb)) {
                    using(var g=Graphics.FromImage(result)) {
                        g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
                        g.DrawImage(bodyImage,bd,br,GraphicsUnit.Pixel);
                        g.DrawImage(shieldImage,sd,sr,GraphicsUnit.Pixel);
                    }
                    result.Save(output,ImageFormat.Png);
                }
                return Path.GetFileName(output)+","+body.Height+","+bs.ToString(System.Globalization.CultureInfo.InvariantCulture)+",264,"+shieldHeight;
            }
        }
    }
    // Produce inspection crops at a common scale; these are not game assets.
    public static void Preview(string input,string output,Rectangle region) {
        using(var b=new Bitmap(input)) using(var c=b.Clone(region,PixelFormat.Format32bppArgb)) c.Save(output,ImageFormat.Png);
    }
}
'@
$dummy.Dispose()
if (Test-Path "$PSScriptRoot/actions_manifest.json") {
    $items = Get-Content "$PSScriptRoot/actions_manifest.json" -Raw | ConvertFrom-Json
    $rows = @('file,source_body_height,uniform_scale,output_body_height,output_total_height')
    foreach ($item in $items) {
        $area = [System.Drawing.Rectangle]::new($item.area[0],$item.area[1],$item.area[2],$item.area[3])
        $body = [System.Drawing.Rectangle]::new($item.body[0],$item.body[1],$item.body[2],$item.body[3])
        if($item.kind -eq 'shield') {
            $rows += [ActionFrames]::PackShield($item.input,$item.output,$area,$body,$item.splitX,$item.handY,$item.shieldHeight)
        } else {
            $rows += [ActionFrames]::Pack($item.input,$item.output,$area,$body,$item.anchorX,$item.targetHeight,$item.scale)
        }
    }
    $rows | Set-Content "$PSScriptRoot/Warrior_ShieldSlash_Actions_v4/validation.csv"
    $rows
}
