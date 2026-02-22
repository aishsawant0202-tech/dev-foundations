/*
//Clear and Resize Array
string[] pallets =  ["B14", "A11", "B12", "A13" ];
Console.WriteLine("");

Array.Clear(pallets, 0, 2);
Console.WriteLine($"Clearing 2 ... count: {pallets.Length}");
foreach (var pallet in pallets)
{
    Console.WriteLine($"-- {pallet}");
}

Console.WriteLine("");
Array.Resize(ref pallets, 6);
Console.WriteLine($"Resizing 6 ... count: {pallets.Length}");

pallets[4] = "C01";
pallets[5] = "C02";

foreach (var pallet in pallets)
{
    Console.WriteLine($"-- {pallet}");
}


// Reverse a whole string using array methods
string value = "abc123";
char[] valueArray = value.ToCharArray();
Console.WriteLine(valueArray);
Array.Reverse(valueArray);
//string result = new string(valueArray);
string result = String.Join(",", valueArray);
Console.WriteLine(result);

// Reverse characters in a string using array methods
string pangram = "The quick brown fox jumps over the lazy dog";
string[] words = pangram.Split(' ');
foreach (string word in words)
{
    char[] valueArray = word.ToCharArray();
    Array.Reverse(valueArray);
    string result = String.Join("", valueArray);
    Console.Write(result + " ");

}*/

//sort orders and tag possible errors using array methods
string orderStream = "B123,C234,A345,C15,B177,G3003,C235,B179";
string[] ordernName = orderStream.Split(',');
Array.Sort(ordernName);
foreach (string name in ordernName)
{ 
    if (name.Length == 4)
    {
        Console.WriteLine(name);
    }
    else
    {
        Console.WriteLine($"{name} \t- Error");

    }

        
}
