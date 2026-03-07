//Catch seperate exception types in code block

// inputValues is used to store numeric values entered by a user
string[] inputValues = new string[] { "three", "9999999999", "0", "2" };

foreach (string inputValue in inputValues)
{
    int numValue = 0;
    try
    {
        numValue = int.Parse(inputValue);
    }
    catch (FormatException ex)
    {
        Console.WriteLine($"Invalid readResult. Please enter a valid number. {ex.Message}");
    }
    catch (OverflowException ex)
    {
        Console.WriteLine($"The number you entered is too large or too small. {ex.Message}");
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
    }
}
/*
try
{
    Process1();
}
catch
{
    Console.WriteLine("An exception has occurred");
}

Console.WriteLine("Exit program");

static void Process1()
{
    try
    {
        WriteMessage();
    }
    catch
    {
        Console.WriteLine("Exception caught in Process1");
    }
}

static void WriteMessage()
{
    double float1 = 3000.0;
    double float2 = 0.0;
    int number1 = 3000;
    int number2 = 0;

    Console.WriteLine(float1 / float2);
    Console.WriteLine(number1 / number2);
}



 string[] names = { "Dog", "Cat", "Fish" };
Object[] objs = (Object[])names;

Object obj = (Object)13;
objs[2] = obj; // ArrayTypeMismatchException occurs

// ----------------------------------------------------
int number1 = 3000;
int number2 = 0;
Console.WriteLine(number1 / number2); // DivideByZeroException occurs

// ----------------------------------------------------

int valueEntered;
string userValue = "two";
valueEntered = int.Parse(userValue); // FormatException occurs

// ----------------------------------------------------


int[] values1 = { 3, 6, 9, 12, 15, 18, 21 };
int[] values2 = new int[6];

values2[values1.Length - 1] = values1[values1.Length - 1]; // IndexOutOfRangeException occurs

// ----------------------------------------------------

object obj = "This is a string";
int num = (int)obj; // InvalidCastException occurs

// ----------------------------------------------------

int[] values = null;
for (int i = 0; i <= 9; i++)
    values[i] = i * 2; // NullReferenceException occurs


// ----------------------------------------------------

decimal x = 400;
byte i;

i = (byte)x; // OverflowException occurs
Console.WriteLine(i);

*/

