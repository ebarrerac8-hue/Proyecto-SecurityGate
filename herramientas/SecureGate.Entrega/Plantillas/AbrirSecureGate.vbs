Option Explicit
Dim shell, fs, folder, exe, token
Set shell = CreateObject("WScript.Shell")
Set fs = CreateObject("Scripting.FileSystemObject")
folder = fs.GetParentFolderName(WScript.ScriptFullName)
exe = fs.BuildPath(folder, "SecureGateIntegrado.exe")
If Not fs.FileExists(exe) Then
    MsgBox "Falta SecureGateIntegrado.exe. Conserva toda la carpeta Cliente.", 16, "SecureGate"
    WScript.Quit 1
End If
token = shell.Environment("USER")("SECUREGATE_CLIENT_TOKEN")
If Len(token) = 0 Then
    MsgBox "Primero ejecuta ConfigurarCliente.cmd.", 48, "SecureGate"
    WScript.Quit 1
End If
shell.Environment("PROCESS")("SECUREGATE_CLIENT_TOKEN") = token
shell.CurrentDirectory = folder
shell.Run Chr(34) & exe & Chr(34), 1, False
