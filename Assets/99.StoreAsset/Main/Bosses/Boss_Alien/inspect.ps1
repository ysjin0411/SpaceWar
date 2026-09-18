Add-Type -AssemblyName System.Drawing
# 이미지 픽셀은 읽기만 하며 원본 PNG는 변경하지 않는다.
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class SpriteInspect {
    public static string Scan(string path) {
        using (var b = new Bitmap(path)) {
            var d = b.LockBits(new Rectangle(0,0,b.Width,b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] bytes = new byte[Math.Abs(d.Stride)*b.Height];
            Marshal.Copy(d.Scan0,bytes,0,bytes.Length);
            b.UnlockBits(d);
            int left=b.Width,top=b.Height,right=-1,bottom=-1;
            int bodyTop=b.Height,footLeft=b.Width,footRight=-1,footBottom=-1;
            long transparent=0,partial=0,solid=0,edge=0;
            for(int y=0;y<b.Height;y++) for(int x=0;x<b.Width;x++){
                byte a=bytes[y*Math.Abs(d.Stride)+x*4+3];
                if(a==0) transparent++; else if(a==255) solid++; else partial++;
                if(a>=128){
                    left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);
                    if(x>500)bodyTop=Math.Min(bodyTop,y);
                    if(y>900){footLeft=Math.Min(footLeft,x);footRight=Math.Max(footRight,x);footBottom=Math.Max(footBottom,y);}
                    if(x==0||y==0||x==b.Width-1||y==b.Height-1)edge++;
                }
            }
            return String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13}", b.Width,b.Height,left,top,right,bottom,transparent,partial,solid,edge,bodyTop,footLeft,footRight,footBottom);
        }
    }
}
'@
$outDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rows = foreach($file in Get-ChildItem -LiteralPath $outDir -Filter '*.png') {
    $v = [SpriteInspect]::Scan($file.FullName).Split(',')
    [pscustomobject]@{name=$file.BaseName; width=[int]$v[0];height=[int]$v[1];left=[int]$v[2];top=[int]$v[3];right=[int]$v[4];bottom=[int]$v[5];transparent=[long]$v[6];partial=[long]$v[7];solid=[long]$v[8];edgePixels=[int]$v[9];bodyTop=[int]$v[10];footLeft=[int]$v[11];footRight=[int]$v[12];footBottom=[int]$v[13]}
}
$rows | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outDir 'validation.json') -Encoding UTF8
$rows | Format-Table name,width,height,left,top,right,bottom,edgePixels,bodyTop,footBottom -AutoSize

