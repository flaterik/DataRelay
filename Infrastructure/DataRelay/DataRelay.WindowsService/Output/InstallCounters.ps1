[string]$runtimeDir = [System.Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()
[string]$installutil = [System.IO.Path]::Combine($runtimeDir, "installutil.exe")
[string]$homeDir = split-path $myInvocation.MyCommand.Path
cd $homedir

invoke-expression "$installutil RelayAssemblies\MySpace.DataRelay.RelayNode.dll"
invoke-expression "$installutil RelayAssemblies\MySpace.DataRelay.RelayComponent.Forwarding.dll"
invoke-expression "$installutil RelayAssemblies\MySpace.SocketTransport.Server.dll"
invoke-expression "$installutil RelayAssemblies\MySpace.Hydration.dll"