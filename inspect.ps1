$asm = [Reflection.Assembly]::LoadFile('E:\duanaminh\AiStudyHub-BE-minhle\bin\Debug\net10.0\Microsoft.OpenApi.dll')
$t = $asm.GetTypes() | Where-Object { $_.Name -eq 'OpenApiSecurityScheme' }
$t.GetProperties() | Select-Object Name, PropertyType | ConvertTo-Json
