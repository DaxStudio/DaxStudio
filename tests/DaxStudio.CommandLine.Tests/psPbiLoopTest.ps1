$query = @"
EVALUATE { COUNTROWS(
FILTER( customer
	, customer[country] = @country
	)
)}
"@

$cmd = "C:\Users\dgosbell\Downloads\DaxStudio_3_1_0_portable\dscmd.exe"
$cmd = "..\..\src\bin\Debug\dscmd.exe"
$server = "powerbi://api.powerbi.com/v1.0/myorg/Fab%20Contoso"
$database = "contoso custom"

$token = &$cmd accesstoken -s $server -d $database  

foreach ( $country in @('AU', 'CA', 'DE', 'FR')) {
		
	Write-Host "Running query for country: $country"
	&$cmd csv c:\temp\myquery-$country.csv -s $server -d $database -p $token  -m country=$country -q $query
}

