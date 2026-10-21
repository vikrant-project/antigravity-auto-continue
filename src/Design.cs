using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace AntiGravityRepeater {
public static class Theme {
 public static Color Background=Color.FromArgb(13,15,21),Surface=Color.FromArgb(22,25,34),Inset=Color.FromArgb(16,19,27),Border=Color.FromArgb(43,47,61),Text=Color.FromArgb(239,240,248),Muted=Color.FromArgb(143,151,172),Purple=Color.FromArgb(167,139,250),Mint=Color.FromArgb(100,221,179);
 public static GraphicsPath Round(Rectangle r,int radius){var p=new GraphicsPath();int d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
}
public class Card : Panel {
 public int Radius=16;
 public Card(){DoubleBuffered=true;BackColor=Theme.Surface;Padding=new Padding(20);}
 protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?Theme.Background:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var p=Theme.Round(new Rectangle(0,0,Width-1,Height-1),Radius)){using(var b=new SolidBrush(BackColor))e.Graphics.FillPath(b,p);using(var pen=new Pen(Theme.Border))e.Graphics.DrawPath(pen,p);}}
}
public class ModernButton : Button {
 public bool Primary;bool hover;
 public ModernButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;Font=new Font("Segoe UI Semibold",10);SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
 protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
 protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Color bg=Primary?(hover?Color.FromArgb(190,168,255):Theme.Purple):(hover?Color.FromArgb(49,53,69):Color.FromArgb(33,37,50));if(!Enabled)bg=Color.FromArgb(29,32,43);using(var path=Theme.Round(new Rectangle(0,0,Width-1,Height-1),10)){using(var b=new SolidBrush(bg))e.Graphics.FillPath(b,path);if(!Primary)using(var pen=new Pen(Theme.Border))e.Graphics.DrawPath(pen,path);}TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,!Enabled?Color.FromArgb(90,97,117):Primary?Color.FromArgb(26,17,48):Theme.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);if(Focused&&ShowFocusCues){var r=ClientRectangle;r.Inflate(-4,-4);ControlPaint.DrawFocusRectangle(e.Graphics,r,Theme.Purple,bg);}}
}
public class NumberControl : UserControl {
 TextBox field=new TextBox();ModernButton minus=new ModernButton(),plus=new ModernButton();decimal value,min,max=100000;
 public decimal Minimum{get{return min;}set{min=value;Value=this.value;}}
 public decimal Maximum{get{return max;}set{max=value;Value=this.value;}}
 public decimal Value{get{Commit();return value;}set{this.value=Math.Max(min,Math.Min(max,value));field.Text=this.value.ToString();}}
 public NumberControl(){Height=38;BackColor=Theme.Inset;field.BorderStyle=BorderStyle.None;field.BackColor=Theme.Inset;field.ForeColor=Theme.Text;field.Font=new Font("Segoe UI Semibold",12);field.TextAlign=HorizontalAlignment.Center;field.AccessibleName="Value";field.Text="0";minus.Text="−";plus.Text="+";minus.Font=plus.Font=new Font("Segoe UI",13);Controls.AddRange(new Control[]{minus,field,plus});minus.Click+=(s,e)=>Value=Value-1;plus.Click+=(s,e)=>Value=Value+1;field.Leave+=(s,e)=>Commit();field.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Enter){Commit();e.SuppressKeyPress=true;}};Resize+=(s,e)=>LayoutParts();LayoutParts();}
 void Commit(){decimal parsed;if(decimal.TryParse(field.Text,out parsed))value=Math.Max(min,Math.Min(max,decimal.Truncate(parsed)));field.Text=value.ToString();}
 void LayoutParts(){minus.SetBounds(0,0,34,Height);plus.SetBounds(Width-34,0,34,Height);field.SetBounds(37,Math.Max(4,(Height-field.PreferredHeight)/2),Math.Max(10,Width-74),field.PreferredHeight);}
}
public class ModernCombo : ComboBox {
 protected override void WndProc(ref Message m){base.WndProc(ref m);if(m.Msg==15 && IsHandleCreated){using(var g=Graphics.FromHwnd(Handle)){g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Theme.Surface);using(var p=Theme.Round(new Rectangle(0,0,Width-1,Height-1),8)){using(var b=new SolidBrush(Theme.Inset))g.FillPath(b,p);using(var pen=new Pen(Focused?Theme.Purple:Theme.Border))g.DrawPath(pen,p);}TextRenderer.DrawText(g,SelectedItem==null?"Select a conversation":SelectedItem.ToString(),Font,new Rectangle(12,0,Width-44,Height),Enabled?Theme.Text:Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);using(var pen=new Pen(Theme.Muted,1.5f))g.DrawLines(pen,new[]{new Point(Width-24,Height/2-2),new Point(Width-19,Height/2+3),new Point(Width-14,Height/2-2)});}}}
 protected override void OnSelectedIndexChanged(EventArgs e){base.OnSelectedIndexChanged(e);Invalidate();}
}
public class ModernCheck : CheckBox {
 public ModernCheck(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);Cursor=Cursors.Hand;AutoSize=false;Size=new Size(480,24);}
 protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;using(var path=Theme.Round(new Rectangle(0,3,29,17),8)){using(var b=new SolidBrush(Checked?Theme.Purple:Theme.Border))g.FillPath(b,path);}using(var b=new SolidBrush(Checked?Theme.Text:Theme.Muted))g.FillEllipse(b,Checked?15:3,6,11,11);TextRenderer.DrawText(g,Text,Font,new Rectangle(40,0,Width-42,Height),Enabled?Theme.Muted:Color.FromArgb(90,97,117),TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(38,0,Width-40,Height));}
}
public class StatusVisual : Control {
 bool running;float angle=-90;System.Windows.Forms.Timer pulse=new System.Windows.Forms.Timer();
 public bool Running {get{return running;}set{if(running==value)return;running=value;if(value)pulse.Start();else pulse.Stop();Invalidate();}}
 public int Count,DelaySeconds=10;public string State="Ready";
 public StatusVisual(){DoubleBuffered=true;BackColor=Theme.Surface;pulse.Interval=40;pulse.Tick+=(s,e)=>{angle=(angle+2)%360;Invalidate();};}
 protected override void Dispose(bool disposing){if(disposing)pulse.Dispose();base.Dispose(disposing);}
 protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float cx=Width/2f,cy=Height/2f;int remaining=0;var match=System.Text.RegularExpressions.Regex.Match(State??"",@"in (\d+)s");bool countdown=match.Success&&int.TryParse(match.Groups[1].Value,out remaining);bool attention=!Running&&new[]{"error","quota","budget","approval","uncertain","unavailable","could not","not observed"}.Any(word=>(State??"").IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0);Color accent=Running?Theme.Mint:attention?Color.FromArgb(240,187,101):Theme.Purple;
 using(var pen=new Pen(Color.FromArgb(38,41,55),8))g.DrawEllipse(pen,cx-52,cy-52,104,104);
 using(var pen=new Pen(accent,4)){pen.StartCap=pen.EndCap=LineCap.Round;g.DrawArc(pen,cx-52,cy-52,104,104,countdown?-90:Running?angle:-90,countdown?Math.Max(3,360f*(1f-remaining/(float)Math.Max(1,DelaySeconds))):Running?100:360);}
 string main=countdown?remaining+"s":Running?"LIVE":attention?"PAUSED":"READY";string sub=countdown?"NEXT PROMPT":Running?"MONITORING":attention?"CHECK STATUS":"PRESS START";
 using(var font=new Font("Segoe UI Semibold",countdown?26:17))TextRenderer.DrawText(g,main,font,new Rectangle((int)cx-56,(int)cy-23,112,40),accent,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
 using(var font=new Font("Segoe UI Semibold",7))TextRenderer.DrawText(g,sub,font,new Rectangle((int)cx-59,(int)cy+18,118,20),Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
 }
}
}
