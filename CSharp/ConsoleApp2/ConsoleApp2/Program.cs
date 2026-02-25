/*string message = "Find what is (inside the parentheses)";
int openingPosition = message.IndexOf('(');
int closingPosition = message.IndexOf(')');
Console.WriteLine(openingPosition);
Console.WriteLine(closingPosition);
int length = closingPosition - openingPosition;
Console.WriteLine(message.Substring(openingPosition, length+1)); 

string message = "What is the value <span>between the tags</span>?";

int openingPosition = message.IndexOf("<span>");
int closingPosition = message.IndexOf("</span>");

openingPosition += 6;
int length = closingPosition - openingPosition;
Console.WriteLine(message.Substring(openingPosition, length)); 

string message = "hello there!";

int first_h = message.IndexOf('h');
int last_h = message.LastIndexOf('h');

Console.WriteLine($"For the message: '{message}', the first 'h' is at position {first_h} and the last 'h' is at position {last_h}.");

//Retrieve all instances of substrings inside parentheses

string message = "(What if) there are (more than) one (set of parentheses)?";
while (true)
{
    int openingPosition = message.IndexOf('(');
    if (openingPosition == -1) break;

    openingPosition += 1;
    int closingPosition = message.IndexOf(')');
    int length = closingPosition - openingPosition;
    Console.WriteLine(message.Substring(openingPosition, length));

    // Note the overload of the Substring to return only the remaining 
    // unprocessed message:
    message = message.Substring(closingPosition + 1);
} 

//Work with different types of symbol sets with IndexOfAny()

//string message = "Hello, world!";
//char[] charsToFind = { 'a', 'e', 'i' };

//int index = message.IndexOfAny(charsToFind);

//Console.WriteLine($"Found '{message[index]}' in '{message}' at index: {index}.");

string message = "Help (find) the {opening symbols}";
Console.WriteLine($"Searching THIS Message: {message}");
char[] openSymbols = { '[', '{', '(' };
int startPosition = 5;
int openingPosition = message.IndexOfAny(openSymbols);
Console.WriteLine($"Found WITHOUT using startPosition: {message.Substring(openingPosition)}");

openingPosition = message.IndexOfAny(openSymbols, startPosition);
Console.WriteLine($"Found WITH using startPosition {startPosition}:  {message.Substring(openingPosition)}"); 

string message = "(What if) I have [different symbols] but every {open symbol} needs a [matching closing symbol]?";

char[] openSymbols = {'[','{','(' };
int closingPosition  = 0;
while (true)
{
    int openPositon = message.IndexOfAny(openSymbols, closingPosition);

    if (openPositon == -1) break;

    string currentSymbol = message.Substring(openPositon,1);

    char matchingSymbol = ' ';

    switch (currentSymbol)
    {
        case "(":
            matchingSymbol = ')';
            break;
        case "{":
            matchingSymbol = '}';
            break;
        case "[":
            matchingSymbol = ']';
            break;
    }
    openPositon += 1;
    closingPosition = message.IndexOf(matchingSymbol, openPositon);

    int length = closingPosition - openPositon;
    Console.WriteLine(message.Substring(openPositon, length));

} */

//Remove characters in specific locations from a string

string data = "12345John Smith          5000  3  ";
string updatedData = data.Remove(5, 20);
Console.WriteLine(updatedData);

string message = "This--is--ex-amp-le--da-ta";
message = message.Replace("--", " ");
message = message.Replace("-", "");
Console.WriteLine(message);