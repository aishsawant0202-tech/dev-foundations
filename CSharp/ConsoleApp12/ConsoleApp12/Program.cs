// With Async
Console.WriteLine("Program started");
var dataTask = GetData();
for (int i = 0; i < 5; i++)
{
    Console.WriteLine($"GetData is running");
}
var result = await dataTask; // Await the task to get the result when it's ready
Console.WriteLine(result);

var number = await GetNumber();
Console.WriteLine("GetData number is running");
Console.WriteLine(number);
static async Task<string>GetData() //Async tells compiler that this method contain asynchronous operations
{
    await Task.Delay(1); 
    // Await tells the program to pause the method until task finishes but does not block the thread.
    return "Data retrieved";
}

static async Task<int>GetNumber()
{
    await Task.Delay(500);
    return 42;
}
/*
//Without Async
var result = GetData();
Console.WriteLine(result);
static string GetData()
{
    Thread.Sleep(1000); // Simulate a delay
    return "Data retrieved";
}
*/
