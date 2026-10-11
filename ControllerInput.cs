using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace HYROLauncherWeb
{
    internal static class ControllerInput
    {
        [StructLayout(LayoutKind.Sequential)] internal struct Pad {public ushort Buttons;public byte LeftTrigger,RightTrigger;public short LeftX,LeftY,RightX,RightY;}
        [StructLayout(LayoutKind.Sequential)] internal struct State {public uint Packet;public Pad Gamepad;}
        [DllImport("xinput1_4.dll",EntryPoint="XInputGetState")] private static extern uint ReadModern(uint index,out State state);
        [DllImport("xinput9_1_0.dll",EntryPoint="XInputGetState")] private static extern uint ReadLegacy(uint index,out State state);
        internal static bool Read(uint index,out State state) {
            try {return ReadModern(index,out state)==0;}catch(DllNotFoundException){try{return ReadLegacy(index,out state)==0;}catch{state=new State();return false;}}catch{state=new State();return false;}
        }
    }
    internal partial class WebLauncherForm
    {
        private bool bigPictureEnabled;
        private Rectangle beforeBigPictureBounds;
        private FormWindowState beforeBigPictureState;
        private Size beforeBigPictureMinimum,beforeBigPictureMaximum;
        private readonly Timer controllerTimer=new Timer();
        private bool controllerConnected;
        private uint lastControllerPacket,lastControllerIndex;
        private void InitializeBigPicture() {
            controllerTimer.Interval=500;controllerTimer.Tick+=delegate {PollController();};
            FormClosed+=delegate {controllerTimer.Stop();controllerTimer.Dispose();};
            Deactivate+=delegate {controllerConnected=false;if(bigPictureEnabled && webView.CoreWebView2!=null)Post(new Dictionary<string,object>{{"type","bigPicturePad"},{"connected",false}});};
        }
        private void SendBigPictureState() {if(webView.CoreWebView2!=null)Post(new Dictionary<string,object>{{"type","bigPicture"},{"enabled",bigPictureEnabled}});}
        private void SetBigPicture(bool enabled) {
            if(enabled==bigPictureEnabled){SendBigPictureState();return;}
            if(enabled) {
                Rectangle screenBounds=Screen.FromControl(this).Bounds;
                beforeBigPictureState=WindowState;beforeBigPictureBounds=WindowState==FormWindowState.Normal?Bounds:RestoreBounds;
                beforeBigPictureMinimum=MinimumSize;beforeBigPictureMaximum=MaximumSize;
                MinimumSize=Size.Empty;MaximumSize=Size.Empty;
                bigPictureEnabled=true;WindowState=FormWindowState.Normal;Bounds=screenBounds;
                ApplyRoundedWindowRegion();controllerConnected=false;controllerTimer.Interval=500;controllerTimer.Start();
            } else {
                bigPictureEnabled=false;controllerTimer.Stop();controllerConnected=false;WindowState=FormWindowState.Normal;
                Bounds=beforeBigPictureBounds;MinimumSize=beforeBigPictureMinimum;MaximumSize=beforeBigPictureMaximum;
                WindowState=beforeBigPictureState;
                if(beforeBigPictureState==FormWindowState.Normal)Bounds=beforeBigPictureBounds;
                ApplyRoundedWindowRegion();
            }
            SendBigPictureState();
        }
        private void PollController() {
            if(!bigPictureEnabled || !Visible || WindowState==FormWindowState.Minimized || Form.ActiveForm!=this || webView.CoreWebView2==null){controllerTimer.Interval=500;return;}
            ControllerInput.State input=new ControllerInput.State();uint slot=0;bool found=false;
            for(uint i=0;i<4;i++)if(ControllerInput.Read(i,out input)){slot=i;found=true;break;}
            controllerTimer.Interval=found?50:500;
            if(found && (!controllerConnected || input.Packet!=lastControllerPacket || slot!=lastControllerIndex)) {
                Post(new Dictionary<string,object>{{"type","bigPicturePad"},{"connected",true},{"buttons",(int)input.Gamepad.Buttons},{"x",input.Gamepad.LeftX/32768.0},{"y",-input.Gamepad.LeftY/32768.0}});
                lastControllerPacket=input.Packet;lastControllerIndex=slot;
            } else if(!found && controllerConnected)Post(new Dictionary<string,object>{{"type","bigPicturePad"},{"connected",false}});
            controllerConnected=found;
        }
    }
}
