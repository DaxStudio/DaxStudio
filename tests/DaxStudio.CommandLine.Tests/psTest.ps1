$query = @"
EVALUATE
FILTER( customer
	, LEFT(customer[First Name], 1) = @letter
)
"@

$cmd = "C:\Users\dgosbell\Downloads\DaxStudio_3_1_0_portable\dscmd.exe"
$cmd = "C:\Users\dgosbell\source\repos\DaxStudio\src\bin\Debug\dscmd.exe"
$server = "localhost\tab19"
$database = "adventure works"
$letter = 'm'

&$cmd csv c:\temp\test-$letter.csv -s $server -d $database -q $query -m letter=$letter
