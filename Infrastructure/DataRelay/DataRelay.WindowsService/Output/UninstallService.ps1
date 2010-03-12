[string]$runtimeDir = [System.Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()
[string]$installutil = [System.IO.Path]::Combine($runtimeDir, "installutil.exe")
[string]$homeDir = split-path $myInvocation.MyCommand.Path
cd $homedir

invoke-expression "$installutil /u MySpace.DataRelay.WindowsService.exe"