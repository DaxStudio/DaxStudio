SET TFM=net472
..\..\src\bin\debug\%TFM%\dscmd vpax c:\temp\test.vpax -s localhost -d "Adventure Works"
..\..\src\bin\debug\%TFM%\dscmd csv c:\temp\test.csv -s localhost -d "Adventure Works" -q "EVALUATE 'Product'"
..\..\src\bin\debug\%TFM%\dscmd xlsx c:\temp\test.xlsx -s localhost -d "Adventure Works" -q "EVALUATE 'Product'"
..\..\src\bin\debug\%TFM%\dscmd export csv c:\temp\cmdexport -s localhost -d "Adventure Works"
