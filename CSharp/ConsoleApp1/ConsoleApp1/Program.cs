using System.Globalization;

int invoiceNumber = 1201;
decimal productShares = 25.4568m;
decimal subtotal = 2750.00m;
decimal taxPercentage = .15825m;
decimal total = 3185.19m;

Console.WriteLine($"Invoice Number: {invoiceNumber}");
Console.WriteLine($"    Shares: {productShares:N3} Product");
Console.WriteLine($"        Subtotal: {subtotal.ToString("C", CultureInfo.GetCultureInfo("en-US"))}");
Console.WriteLine($"        Tax: {taxPercentage:P2}");
Console.WriteLine($"        Total: {total.ToString("C", CultureInfo.GetCultureInfo("en-US"))}");
Console.WriteLine("\n");

Console.WriteLine("---------------------------------------------------");
//Padding and alignment

/*
Console.WriteLine("\n");
string input = "Pad this";
Console.WriteLine(input.PadLeft(12, '-'));
Console.WriteLine(input.PadRight(12,'-'));*/

//store the Payment ID in columns 1 through 6, the payee's name in columns 7 through 30,
//and the Payment Amount in columns 31 through 40. Also, importantly, the Payment Amount is right-aligned.

/*string paymentId = "769C";
string payeeName = "Mr. Stephen Ortega";
string paymentAmount = "$5,000.00";

var formattedLine = paymentId.PadRight(6);
formattedLine += payeeName.PadRight(24);
formattedLine += paymentAmount.PadLeft(10);

Console.WriteLine(formattedLine); */
Console.WriteLine("\n");

string customerName = "Ms. Barros";

string currentProduct = "Magic Yield";
int currentShares = 2975000;
decimal currentReturn = 0.1275m;
decimal currentProfit = 55000000.0m;

string newProduct = "Glorious Future";
decimal newReturn = 0.13125m;
decimal newProfit = 63000000.0m;

Console.WriteLine($"Dear {customerName},");
Console.WriteLine("\n");
Console.WriteLine($"As a customer of our {currentProduct} offering we are excited to tell you about a new financial product that would dramatically increase your return.");
Console.WriteLine($"Currently, you own {currentShares.ToString("C", CultureInfo.GetCultureInfo("en-US"))} shares at a return of {currentReturn.ToString("P2")}.");
Console.WriteLine($"Our new product, {newProduct} offers a return of {newReturn.ToString("P2")}. Given your current volume, your potential profit would be {newProfit.ToString("C",CultureInfo.GetCultureInfo("en-US"))}");
Console.WriteLine("\n");

Console.WriteLine("Here's a quick comparison:\n");
var formattedLine = currentProduct.PadRight(20);
formattedLine += currentReturn.ToString("P2").PadRight(10);
formattedLine += currentProfit.ToString("C", CultureInfo.GetCultureInfo("en-US")).PadLeft(15);
Console.WriteLine(formattedLine);


string comparisonMessage = "";

comparisonMessage = newProduct.PadRight(20);
comparisonMessage += newReturn.ToString("P2").PadRight(10);
comparisonMessage += newProfit.ToString("C", CultureInfo.GetCultureInfo("en-US")).PadLeft(15);

Console.WriteLine(comparisonMessage);


Console.WriteLine("C110".PadLeft(6, '0'));
