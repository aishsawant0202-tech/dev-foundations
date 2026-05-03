
// TimelineHelper and TimelineState not been used after updating code
// TimelineResolve() never called � ReminderTool computes timeline state inline using a date switch expression
//The enum is only referenced by TimelineHelper (also deleted); Asset.TimelineState is stored as a plain string throughout
/* 
using ReminderAgent.Models;
using ReminderAgent.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace UnitTest_ReminderAgent.Sprint_2;

[TestClass]
public class TimelineHelperTests
{
    [TestMethod]
    public void Resolve_ReturnsPast_WhenInputContainsYesterday()
    {
        // Arrange
        string input = "I went to the restaurant yesterday";

        // Act
        TimelineState result = TimelineHelper.Resolve(input);

        // Assert
        Assert.AreEqual(TimelineState.Past, result);
    }

    [TestMethod]
    public void Resolve_ReturnsFuture_WhenInputContainsTomorrow()
    {
        // Arrange
        string input = "I will travel tomorrow";

        // Act
        TimelineState result = TimelineHelper.Resolve(input);

        // Assert
        Assert.AreEqual(TimelineState.Future, result);
    }

    [TestMethod]
    public void Resolve_ReturnsPresent_WhenInputIsCurrentFact()
    {
        // Arrange
        string input = "My favorite book is Clean Code";

        // Act
        TimelineState result = TimelineHelper.Resolve(input);

        // Assert
        Assert.AreEqual(TimelineState.Present, result);
    }
}
*/
