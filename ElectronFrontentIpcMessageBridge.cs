using System;
using System.Linq;
using ElectronNET.API;
using Foxpict.Client.Sdk.Infra;

namespace foxpict.client.app.Core {
  public class ElectronFrontentIpcMessageBridge : IFrontendIpcMessageBridge {
    public void RegisterEventHandler (string ipcEventName, Action<object> receiveHandler) {
      Electron.IpcMain.On(ipcEventName, receiveHandler);
    }

    public void Send (string ipcEventName, IpcMessage param) {
      var mainWindow = Electron.WindowManager.BrowserWindows.First ();
      Electron.IpcMain.Send (mainWindow, ipcEventName, param);
    }
  }
}
