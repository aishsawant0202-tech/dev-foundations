/*
//Without LINQ
List<int> numbers1 = new List<int>() { 1, 2, 3, 4, 5, 6 };
foreach (var num in numbers1)
{
    if (num % 2 == 0)
    {
        Console.WriteLine(num);
    }

}

//With LINQ
List<int> numbers = new List<int>() {1,2,3,4,5,6};
var evenNumbers = numbers.Where(n => n % 2 == 0);
foreach (var num in evenNumbers)
{
    Console.WriteLine(num);
}

// -----------------------------------------------------------------------------

// List<string> namrs = new List<string>(); //This is LINQ to Objects

// -----------------------------------------------------------------------------

// ** Two Syntaxes of LINQ **
List<int> numbers = new List<int>() { 1, 2, 3, 4, 5, 6 };
// Query Syntax (SQL - like)
var result =
    from n in numbers
    where n > 3
    select n; 

//Method Syntax (Lambda Expressions)
var result2 = numbers.Where(n => n > 3);
foreach (var num in result2)
{
    Console.WriteLine(num); 
} */
/*
// * LINQ Execution Types *

// Deferred Execution in LINQ - Query runs only when enumerated

List<int> numbers = new List<int>() { 1, 2, 3, 4, 5, 6 };
var evenNumbers = numbers.Where(n => n % 2 == 0); // Query is defined but not executed
numbers.Add(8); // Modifying the source collection before enumeration
foreach (var num in evenNumbers)
{
    Console.WriteLine(num); // Query executes here
}

// Immediate Execution in LINQ - Query runs immediately and results are stored

var oddNumbers = numbers.Where(n => n % 2 != 0).ToList(); // Query executes immediately and results are stored in a list
numbers.Add(9); // since query has already executed, this modification does not affect the results
foreach (var num in oddNumbers)
{
    Console.WriteLine(num); // No query execution here, results are already stored
}
*/

//With OfType<T>() method in LINQ, you can filter elements of a specific type from a collection.
//This is particularly useful when working with collections that contain multiple types or when you want to ensure that only elements of a certain type are processed.

using System;

List<int> numbers = new List<int>() { 5, 3, 1, 6, 4, 2, 10, 9, 7};
var evenNumbers = numbers.OfType<int>().Where(n => n % 2 == 0);
Console.WriteLine($"\nEven numbers:\n");
foreach (var num in evenNumbers)
{
    Console.WriteLine($"{num}"); 

}

// With Select() method in LINQ, you can project each element of a collection into a new form.
var squareNumbers = numbers.Select(n => n * n);
Console.WriteLine($"\nSquare of of all numbers:\n");
foreach (var num in squareNumbers)
{
    Console.WriteLine($"{num}"); //This will print the squares of all numbers in the original list
}

// You can also combine Select() with Where() to filter and project data in a single query.
var squareNumbers2 = numbers.Select(n => n * n).Where(n => n > 10);
Console.WriteLine($"\nSquare of even numbers:\n");
foreach (var num in squareNumbers2)
{
    Console.WriteLine($"{num}" ); //This will print the squares of all numbers that are greater than 10
}
// With SelectMany() method in LINQ, you can project each element of a collection into an IEnumerable<T> and flatten the resulting sequences into a single sequence.
var squareNumbers3 = numbers.SelectMany(n => new List<int> { n, n * n });
Console.WriteLine($"\nNumbers and their squares:\n");
foreach (var num in squareNumbers3)
{ 
    Console.WriteLine($"{num}"); //This will print both the original numbers and their squares in a single sequence
}


var sortedNumbers = numbers.OrderBy(n => n);
Console.WriteLine($"\nAscending order:\n");
foreach (var num in sortedNumbers)
{
    Console.WriteLine($"{num}"); //This will print the numbers in ascending order
}

var totalNumbers = numbers.Count();
Console.WriteLine($"\nTotal numbers: {totalNumbers}"); //This will print the total count of numbers in the list

var MaxNumber = numbers.Max();
Console.WriteLine($"\nMaximum number: {MaxNumber}"); //This will print the maximum number in the list

var MinNumber = numbers.Min();
Console.WriteLine($"\nMinimum number: {MinNumber}"); //This will print the minimum number in the list

var averageNumber = numbers.Average();
Console.WriteLine($"\nAverage number: {averageNumber}"); //This will print the average of the numbers in the list

var sumNumbers = numbers.Sum();
Console.WriteLine($"\nSum of numbers: {sumNumbers}"); //This will print the sum of the numbers in the list

var FirstNumber = numbers.First();
Console.WriteLine($"\nFirst number: {FirstNumber}"); //This will print the first number in the list

var LastNumber = numbers.Last();
Console.WriteLine($"\nLast number: {LastNumber}"); //This will print the last number in the list

var FirstEvenNumber = numbers.First(n => n % 2 == 0);
Console.WriteLine($"\nFirst even number: {FirstEvenNumber}"); //This will print the first even number in the list

var firstorDefaultEvenNumber = numbers.FirstOrDefault(n => n % 2 == 0);
Console.WriteLine($"\nFirst even number or default: {firstorDefaultEvenNumber}"); //This will print the first even number in the list, or 0 if there are no even numbers

var singleNumber = numbers.Single();
Console.WriteLine($"\nSingle number: {singleNumber}"); //This will throw an exception because there are multiple numbers in the list





