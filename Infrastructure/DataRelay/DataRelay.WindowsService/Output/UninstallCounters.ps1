[string]$runtimeDir = [System.Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()
[string]$installutil = [System.IO.Path]::Combine($runtimeDir, "installutil.exe")
[string]$homeDir = split-path $myInvocation.MyCommand.Path
cd $homedir

invoke-expression "$installutil /u RelayAssemblies\MySpace.DataRelay.RelayNode.dll"
invoke-expression "$installutil /u RelayAssemblies\MySpace.DataRelay.RelayComponent.Forwarding.dll"
invoke-expression "$installutil /u RelayAssemblies\MySpace.SocketTransport.Server.dll"
invoke-expression "$installutil /u RelayAssemblies\MySpace.Hydration.dll"