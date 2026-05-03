using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;
using ReminderAgent.Domain;
using ReminderAgent.Interfaces;
using ReminderAgent.Tools;

namespace UnitTest_SprintSix
{
    /// <summary>
    /// Contains comprehensive unit tests for validating the behavior of the ReminderTool,
    /// including asset creation, retrieval, filtering, and utility functions.
    /// </summary>
    [TestClass]
    public class ReminderToolTests
    {
        /// <summary>Mock storage provider.</summary>
        private Mock<IStorageProvider> _mockStorage = null!;
        /// <summary>Mock embedding service.</summary>
        private Mock<IEmbeddingService> _mockEmbedding = null!;
        /// <summary>Mock similarity service.</summary>

        private Mock<ISimilarityService> _mockSimilarity = null!;
        /// <summary>Mock chat client.</summary>
        private Mock<IChatClient> _mockChat = null!;
        /// <summary>System under test.</summary>
        private ReminderTools _tool = null!;

        /// <summary>
        // /// Initializes mocks and default behaviors before each test.
        // /// </summary>

        [TestInitialize]
        public void Setup()
        {
            _mockStorage = new Mock<IStorageProvider>();
            _mockEmbedding = new Mock<IEmbeddingService>();
            _mockSimilarity = new Mock<ISimilarityService>();
            _mockChat = new Mock<IChatClient>();

            // EmbeddingService: return 1536-dim zero vector
            _mockEmbedding
                .Setup(e => e.GenerateEmbeddingAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new float[1536]);

            _mockEmbedding
                .Setup(e => e.GenerateQueryEmbeddingAsync(It.IsAny<string>()))
                .ReturnsAsync(new float[1536]);

            // SimilarityService: return empty list by default
            _mockSimilarity
                .Setup(s => s.GetTopKSimilarAsync(
                    It.IsAny<float[]>(),
                    It.IsAny<IEnumerable<Asset>>(),
                    It.IsAny<int>(),
                    It.IsAny<float>()))
                .ReturnsAsync(new List<Asset>());

            // Chat client echoes system message context  
            _mockChat
                .Setup(c => c.GetResponseAsync(
                    It.IsAny<IList<ChatMessage>>(),
                    It.IsAny<ChatOptions?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((IList<ChatMessage> messages,
                               ChatOptions? opts,
                               CancellationToken ct) =>
                {
                    // Find the system message — it contains the asset context
                    var systemMsg = messages
                        .FirstOrDefault(m => m.Role == ChatRole.System);

                    // Use system message content as response (contains asset names)
                    string responseText = systemMsg?.Text
                        ?? "Mock RAG response from LLM";

                    return new ChatResponse(new List<ChatMessage>
                    {
                        new ChatMessage(ChatRole.Assistant, responseText)
                    });
                });

            // Storage: default empty list, save returns true
            _mockStorage
                .Setup(s => s.GetAssetsAsync())
                .ReturnsAsync(new List<Asset>());

            _mockStorage
                .Setup(s => s.SaveAssetAsync(It.IsAny<Asset>()))
                .ReturnsAsync(true);

            // Build ReminderTool with all mocks
            _tool = new ReminderTools(
                _mockStorage.Object,
                _mockEmbedding.Object,
                _mockSimilarity.Object,
                _mockChat.Object);
        }
        /// <summary>
        /// Creates a test asset with customizable properties.
        /// </summary>
        private static Asset MakeAsset(
            string name = "Test Asset",
            string category = "Book",
            DateTime? eventDate = null,
            string city = "Frankfurt",
            string country = "Germany",
            string region = "Hesse",
            string timeline = "Past",
            string experience = "Good",
            List<string>? photos = null,
            List<string>? tags = null)
        {
            return new Asset
            {
                Id = Guid.NewGuid(),
                Name = name,
                Category = category,
                UserInput = $"I experienced {name}",
                UserExperience = experience,
                EventDate = eventDate ?? DateTime.Today.AddMonths(-1),
                CreatedAt = DateTime.Now,
                TimelineState = timeline,
                Embedding = new float[1536],
                Tags = tags ?? new List<string> { "test" },
                PhotoRefs = photos ?? new List<string>(),
                Metadata = new Dictionary<string, string>
                {
                    { "Info",    "Test notes" },
                    { "City",    city         },
                    { "Country", country      },
                    { "Region",  region       }
                }
            };
        }
        /// <summary>
        /// Invokes the private ParseDateRange method using reflection.
        /// </summary>
        private (DateTime? from, DateTime? to) CallParseDateRange(string input)
        {
            var m = typeof(ReminderTools)
                .GetMethod("ParseDateRange",
                    BindingFlags.NonPublic | BindingFlags.Instance)!;
            return ((DateTime?, DateTime?))m.Invoke(_tool, new object[] { input })!;
        }
        /// <summary>
        /// Invokes the private AssetMatchesLocation method using reflection.
        /// </summary>
        private bool CallAssetMatchesLocation(Asset asset, string location)
        {
            var m = typeof(ReminderTools)
                .GetMethod("AssetMatchesLocation",
                    BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (bool)m.Invoke(_tool, new object[] { asset, location })!;
        }
        /// <summary>
        /// Invokes the private IsAbsolutePath method using reflection.
        /// </summary>
        private bool CallIsAbsolutePath(string path)
        {
            var m = typeof(ReminderTools)
                .GetMethod("IsAbsolutePath",
                    BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (bool)m.Invoke(_tool, new object[] { path })!;
        }
        /// <summary>
        // /// Verifies that creating an asset with valid parameters returns a success message.
        // /// </summary>
        [TestMethod]
        public async Task TC01_CreateAsset_AllValidParams_ReturnsSuccessMessage()
        {
            // ARRANGE
            _mockStorage
                .Setup(s => s.SaveAssetAsync(It.IsAny<Asset>()))
                .ReturnsAsync(true);
            // ACT
            var result = await _tool.CreateAsset(
                name: "Atomic Habits",
                category: "Book",
                userInput: "I finished Atomic Habits last month — life changing",
                userExperience: "Wonderful",
                tags: "habits,productivity,self-improvement",
                metadata: "Author: James Clear",
                timeContext: "last month",
                eventDate: DateTime.Today.AddMonths(-1).ToString("dd-MM-yyyy"),
                photoRefs: null,
                city: "Frankfurt",
                country: "Germany",
                region: "Hesse");
            // ASSERT
            result.Should().Contain("Successfully remembered",
                because: "a valid save must return the success phrase");
            result.Should().Contain("Atomic Habits");
            result.Should().Contain("Book");

            _mockStorage.Verify(s => s.SaveAssetAsync(It.IsAny<Asset>()), Times.Once);
        }
        /// <summary>
        /// Verifies that invalid dates fall back to today's date.
        /// </summary>
        [TestMethod]
        public async Task TC02_CreateAsset_InvalidDate_FallsBackToToday()
        {
            // ARRANGE
            Asset? captured = null;
            _mockStorage
                .Setup(s => s.SaveAssetAsync(It.IsAny<Asset>()))
                .Callback<Asset>(a => captured = a)
                .ReturnsAsync(true);
            // ACT
            var result = await _tool.CreateAsset(
                name: "Mystery Event",
                category: "Event",
                userInput: "Something happened",
                userExperience: "Interesting",
                eventDate: "not-a-date");
            // ASSERT
            result.Should().Contain("Successfully remembered");
            captured!.EventDate.Should().BeCloseTo(DateTime.Today,
                TimeSpan.FromSeconds(5),
                because: "failed date parse must fall back to DateTime.Today");
        }
        /// <summary>
        /// TC03: Verifies that when user experience is "N/A",
        /// it is normalized to "Neutral" before saving the asset.
        /// </summary>
        [TestMethod]
        public async Task TC03_CreateAsset_NAExperience_NormalisesToNeutral()
        {
            // ARRANGE
            Asset? captured = null;
            _mockStorage
                .Setup(s => s.SaveAssetAsync(It.IsAny<Asset>()))
                .Callback<Asset>(a => captured = a)
                .ReturnsAsync(true);
            // ACT
            await _tool.CreateAsset(
                name: "Book X",
                category: "Book",
                userInput: "I read this",
                userExperience: "N/A");
            // ASSERT
            captured!.UserExperience.Should().Be("Neutral",
                because: "N/A must be normalised to Neutral before saving");
        }
        /// <summary>
        /// TC04: Verifies that empty user experience values
        /// are normalized to "Neutral".
        /// </summary>
        [TestMethod]
        public async Task TC04_CreateAsset_EmptyExperience_NormalisesToNeutral()
        {
            // ARRANGE
            Asset? captured = null;
            _mockStorage
                .Setup(s => s.SaveAssetAsync(It.IsAny<Asset>()))
                .Callback<Asset>(a => captured = a)
                .ReturnsAsync(true);
            // ACT
            await _tool.CreateAsset(
                name: "Book Y",
                category: "Book",
                userInput: "I read this",
                userExperience: "");
            // ASSERT
            captured!.UserExperience.Should().Be("Neutral",
                because: "empty string experience must normalise to Neutral");
        }
        /// <summary>
        /// TC05: Verifies that a future event date
        /// results in TimelineState being set to "Future".
        /// </summary>
        [TestMethod]
        public async Task TC05_CreateAsset_FutureDate_TimelineStateIsFuture()
        {
            // ARRANGE
            Asset? captured = null;
            _mockStorage
                .Setup(s => s.SaveAssetAsync(It.IsAny<Asset>()))
                .Callback<Asset>(a => captured = a)
                .ReturnsAsync(true);
            // ACT
            var result = await _tool.CreateAsset(
                name: "Upcoming Concert",
                category: "Event",
                userInput: "Concert next week",
                userExperience: "Excited",
                eventDate: DateTime.Today.AddDays(7).ToString("dd-MM-yyyy"));
            // ASSERT
            captured!.TimelineState.Should().Be("Future",
                because: "future date must produce TimelineState = Future");
            result.Should().Contain("Future");
        }
        /// <summary>
        /// TC06: Verifies that today's date
        /// results in TimelineState being set to "Present".
        /// </summary>
        [TestMethod]
        public async Task TC06_CreateAsset_TodayDate_TimelineStateIsPresent()
        {
            // ARRANGE
            Asset? captured = null;
            _mockStorage
                .Setup(s => s.SaveAssetAsync(It.IsAny<Asset>()))
                .Callback<Asset>(a => captured = a)
                .ReturnsAsync(true);
            // ACT
            await _tool.CreateAsset(
                name: "Today Lunch",
                category: "Restaurant",
                userInput: "Having lunch today",
                userExperience: "Good",
                eventDate: DateTime.Today.ToString("dd-MM-yyyy"));
            // ASSERT
            captured!.TimelineState.Should().Be("Present",
                because: "today's date must produce TimelineState = Present");
        }
        /// <summary>
        /// TC07: Verifies that tags are split, trimmed,
        /// and empty entries are removed correctly.
        /// </summary>
        [TestMethod]
        public async Task TC07_CreateAsset_TagsWithWhitespace_SplitAndTrimmedCleanly()
        {
            // ARRANGE
            Asset? captured = null;
            _mockStorage
                .Setup(s => s.SaveAssetAsync(It.IsAny<Asset>()))
                .Callback<Asset>(a => captured = a)
                .ReturnsAsync(true);
            // ACT
            await _tool.CreateAsset(
                name: "Atomic Habits",
                category: "Book",
                userInput: "I read this",
                userExperience: "Great",
                tags: "habits, productivity , , self-improvement");
            // ASSERT
            captured!.Tags.Should().HaveCount(3,
                because: "empty entries from double-commas must be removed");
            captured.Tags.Should().Contain("productivity",
                because: "spaces around tags must be trimmed");
            captured.Tags.Should().NotContain("",
                because: "empty tags must be filtered out");
        }
        /// <summary>
        /// TC08: Verifies that duplicate asset save attempts
        /// return a user-friendly error message.
        /// </summary>
        [TestMethod]
        public async Task TC08_CreateAsset_DuplicateSave_ReturnsFriendlyError()
        {
            // ARRANGE
            _mockStorage
                .Setup(s => s.SaveAssetAsync(It.IsAny<Asset>()))
                .ThrowsAsync(new InvalidOperationException("Duplicate asset detected."));
            // ACT
            var result = await _tool.CreateAsset(
                name: "Duplicate Book",
                category: "Book",
                userInput: "I read this before",
                userExperience: "Good");
            // ASSERT
            result.Should().Contain("Could not save",
                because: "InvalidOperationException must be caught as friendly message");
            result.Should().Contain("Duplicate Book");
            result.Should().NotContain("Successfully remembered");
        }
        
        /// <summary>
        // /// GROUP B: GetReminders Tests
        // /// </summary>
        // 
        // /// <summary>
        // /// TC09: Verifies that when filtering by "Past",
        // /// only past assets are returned.
        // /// </summary>
        [TestMethod]
        public async Task TC09_GetReminders_PastFilter_OnlyPastAssetsReturned()
        {
            // ARRANGE
            var pastAsset = MakeAsset("Past Book", eventDate: DateTime.Today.AddMonths(-2), timeline: "Past");
            var futureAsset = MakeAsset("Future Trip", eventDate: DateTime.Today.AddMonths(3), timeline: "Future");

            _mockStorage
                .Setup(s => s.GetAssetsAsync())
                .ReturnsAsync(new List<Asset> { pastAsset, futureAsset });
            // ACT
            var result = await _tool.GetReminders(state: "Past");
            // ASSERT
            result.Should().Contain("Past Book",
                because: "past asset must be in the RAG context passed to chat client");
            result.Should().NotContain("Future Trip",
                because: "future asset was filtered out before context was built");
        }
        /// <summary>
        /// TC10: Verifies that when filtering by "Future",
        /// only future assets are returned.
        /// </summary>
        [TestMethod]
        public async Task TC10_GetReminders_FutureFilter_OnlyFutureAssetsReturned()
        {
            // ARRANGE
            var pastAsset = MakeAsset("Old Book", eventDate: DateTime.Today.AddMonths(-1), timeline: "Past");
            var futureAsset = MakeAsset("Upcoming Event", eventDate: DateTime.Today.AddMonths(2), timeline: "Future");

            _mockStorage
                .Setup(s => s.GetAssetsAsync())
                .ReturnsAsync(new List<Asset> { pastAsset, futureAsset });
            // ACT
            var result = await _tool.GetReminders(state: "Future");
            // ASSERT
            result.Should().Contain("Upcoming Event",
                because: "future asset must be in the RAG context for Future filter");
            result.Should().NotContain("Old Book",
                because: "past asset was filtered out before context was built");
        }
        /// <summary>
        /// TC11: Verifies that when no state filter is provided,
        /// all assets are returned.
        /// </summary>
        [TestMethod]
        public async Task TC11_GetReminders_NullState_ReturnsAllAssets()
        {
            // ARRANGE
            _mockStorage
                .Setup(s => s.GetAssetsAsync())
                .ReturnsAsync(new List<Asset>
                {
                    MakeAsset("Past Asset",    eventDate: DateTime.Today.AddDays(-10), timeline: "Past"),
                    MakeAsset("Present Asset", eventDate: DateTime.Today,              timeline: "Present"),
                    MakeAsset("Future Asset",  eventDate: DateTime.Today.AddDays(10),  timeline: "Future"),
                });
            // ACT
            var result = await _tool.GetReminders(state: null);
            // ASSERT
            result.Should().Contain("Past Asset")
                  .And.Contain("Present Asset")
                  .And.Contain("Future Asset",
                      because: "all 3 assets must appear in the RAG context for null filter");
        }
        /// <summary>
        /// TC12: Verifies that stale "Future" timeline states
        /// are recalculated correctly to "Past".
        /// </summary>
        [TestMethod]
        public async Task TC12_GetReminders_StaleFutureState_RecalculatedToPast()
        {
            // ARRANGE — stale TimelineState="Future" but date is yesterday
            var staleAsset = MakeAsset("Stale Booking",
                eventDate: DateTime.Today.AddDays(-1),
                timeline: "Future");  // ← stale / wrong in CSV

            _mockStorage
                .Setup(s => s.GetAssetsAsync())
                .ReturnsAsync(new List<Asset> { staleAsset });
            // ACT
            var result = await _tool.GetReminders(state: "Past");
            // ASSERT
            result.Should().Contain("Stale Booking",
                because: "stale Future state must be overridden by dynamic recalculation " +
                         "so the asset appears in Past filter results and RAG context");
        }
        /// <summary>
        /// TC13: Verifies that an empty storage
        /// returns a "no results" message.
        /// </summary>
        [TestMethod]
        public async Task TC13_GetReminders_EmptyStorage_ReturnsNoResultsMessage()
        {
            // ARRANGE
            _mockStorage
                .Setup(s => s.GetAssetsAsync())
                .ReturnsAsync(new List<Asset>());
            // ACT
            var result = await _tool.GetReminders(state: "Past");
            // ASSERT
            result.Should().Contain("no",
                because: "empty storage returns early with 'I found no...' message " +
                         "before any chat client call");
        }
        /// GROUP C — ParseDateRange Tests
        
        /// <summary>
        /// TC14: Verifies that "last week" returns the correct previous calendar week range (Monday to Sunday).
        /// </summary>
        [TestMethod]
        public void TC14_ParseDateRange_LastWeek_ReturnsPreviousCalendarWeek()
        {
            // ACT
            var (from, to) = CallParseDateRange("last week");
            // ASSERT
            from.Should().NotBeNull(because: "'last week' must produce a non-null from date");
            to.Should().NotBeNull(because: "'last week' must produce a non-null to date");
            // Calculate expected calendar week boundaries
            int daysFromMonday = ((int)DateTime.Today.DayOfWeek + 6) % 7;
            DateTime thisMonday = DateTime.Today.AddDays(-daysFromMonday);
            DateTime lastMonday = thisMonday.AddDays(-7);   // Monday of last week
            DateTime lastSunday = thisMonday.AddDays(-1);   // Sunday of last week

            from!.Value.Date.Should().Be(lastMonday.Date,
                because: "from must be Monday of the previous calendar week");
            to!.Value.Date.Should().Be(lastSunday.Date,
                because: "to must be Sunday of the previous calendar week");
        }
        /// <summary>
        /// TC15: Verifies that "last month" returns the full previous calendar month range.
        /// </summary>
        [TestMethod]
        public void TC15_ParseDateRange_LastMonth_ReturnsPreviousCalendarMonth()
        {
            // ACT
            var (from, to) = CallParseDateRange("last month");
            // ASSERT
            from.Should().NotBeNull(because: "'last month' must produce a non-null from date");
            to.Should().NotBeNull(because: "'last month' must produce a non-null to date");
            // Calculate expected calendar month boundaries
            DateTime firstOfThisMonth = new DateTime(
                DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime firstOfLastMonth = firstOfThisMonth.AddMonths(-1);
            DateTime lastOfLastMonth = firstOfThisMonth.AddDays(-1);

            from!.Value.Date.Should().Be(firstOfLastMonth.Date,
                because: "from must be the 1st day of the previous calendar month");
            to!.Value.Date.Should().Be(lastOfLastMonth.Date,
                because: "to must be the last day of the previous calendar month");
        }
        /// <summary>
        /// TC16: Verifies that numeric inputs (e.g., "last 3 months") return correct calendar-aligned ranges.
        /// </summary>
        [TestMethod]
        [DataRow("last 3 months", 3)]
        [DataRow("last 6 months", 6)]
        public void TC16_ParseDateRange_DigitNumbers_ReturnsCalendarMonthBoundaries(
            string input, int n)
        {
            // ACT
            var (from, to) = CallParseDateRange(input);
            // ASSERT
            from.Should().NotBeNull(because: $"'{input}' must produce a from date");
            DateTime firstOfThisMonth = new DateTime(
                DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime expectedFrom = firstOfThisMonth.AddMonths(-n);
            DateTime expectedTo = firstOfThisMonth.AddDays(-1);

            from!.Value.Date.Should().Be(expectedFrom.Date,
                because: $"'{input}' from must be 1st of month {n} months ago");
            to!.Value.Date.Should().Be(expectedTo.Date,
                because: $"'{input}' to must be last day of last month");
        }
        /// <summary>
        /// TC17: Verifies that written numbers (e.g., "last two months") are correctly interpreted.
        /// </summary>
        [TestMethod]
        [DataRow("last two months", 2)]
        [DataRow("last three months", 3)]
        [DataRow("last one month", 1)]
        public void TC17_ParseDateRange_WrittenWordNumbers_CalendarBoundariesVerified(
            string input, int n)
        {
            // ACT
            var (from, to) = CallParseDateRange(input);

            // ASSERT
            from.Should().NotBeNull(
                because: $"'{input}' must produce a non-null from date");

            DateTime firstOfThisMonth = new DateTime(
                DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime expectedFrom = firstOfThisMonth.AddMonths(-n);
            DateTime expectedTo = firstOfThisMonth.AddDays(-1);

            from!.Value.Date.Should().Be(expectedFrom.Date,
                because: $"'{input}' must go back {n} calendar month(s) " +
                         $"from the 1st of this month — calendar boundary logic");
            to!.Value.Date.Should().Be(expectedTo.Date,
                because: $"'{input}' to date must be last day of last month");
        }
        /// <summary>
        /// TC18: Verifies that unrecognized input returns null date range.
        /// </summary>
        [TestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("some random words")]
        [DataRow("whenever")]
        public void TC18_ParseDateRange_UnrecognisedInput_ReturnsNullNull(string input)
        {
            // ACT
            var (from, to) = CallParseDateRange(input);

            // ASSERT
            from.Should().BeNull(because: $"unrecognised '{input}' must return null from");
            to.Should().BeNull(because: $"unrecognised '{input}' must return null to");
        }
        /// <summary>
        /// TC19: Verifies that an asset matches when the city name matches exactly.
        /// </summary>
        [TestMethod]
        public void TC19_AssetMatchesLocation_CityForwardMatch_ReturnsTrue()
        {
            var asset = MakeAsset(city: "Frankfurt", country: "Germany", region: "Hesse");
            CallAssetMatchesLocation(asset, "Frankfurt")
                .Should().BeTrue(because: "city name must match forward");
        }
        /// <summary>
        /// TC20: Verifies that an asset matches when the country name matches.
        /// </summary>
        [TestMethod]
        public void TC20_AssetMatchesLocation_CountryMatch_ReturnsTrue()
        {
            var asset = MakeAsset(city: "Frankfurt", country: "Germany", region: "Hesse");
            CallAssetMatchesLocation(asset, "Germany")
                .Should().BeTrue(because: "country name must match");
        }
        /// <summary>
        /// TC21: Verifies reverse containment matching for composite location input.
        /// </summary>
        [TestMethod]
        public void TC21_AssetMatchesLocation_ReverseContainment_ReturnsTrue()
        {
            var asset = MakeAsset(city: "Frankfurt", country: "Germany", region: "Hesse");
            CallAssetMatchesLocation(asset, "Frankfurt, Germany")
                .Should().BeTrue(because: "query containing stored city must match via reverse containment");
        }
        /// <summary>
        /// TC22: Verifies that non-matching locations return false.
        /// </summary>
        [TestMethod]
        public void TC22_AssetMatchesLocation_NoMatch_ReturnsFalse()
        {
            var asset = MakeAsset(city: "Frankfurt", country: "Germany", region: "Hesse");
            CallAssetMatchesLocation(asset, "Rome")
                .Should().BeFalse(because: "Rome must not match a Frankfurt asset");
        }
        /// <summary>
        /// TC23: Verifies detection of Windows absolute file paths.
        /// </summary>
        [TestMethod]
        [DataRow(@"C:\Photos\paris.jpg", true)]
        [DataRow(@"D:\Users\photo.png", true)]
        [DataRow(@"\\server\share\img.jpg", true)]
        public void TC23_IsAbsolutePath_WindowsPaths_ReturnsTrue(
            string path, bool expected)
        {
            CallIsAbsolutePath(path)
                .Should().Be(expected, because: $"'{path}' is a Windows absolute path");
        }
        /// <summary>
        /// TC24: Verifies detection of Unix absolute file paths.
        /// </summary>
        [TestMethod]
        [DataRow("/home/user/photo.png", true)]
        [DataRow("/var/images/img.jpg", true)]
        public void TC24_IsAbsolutePath_UnixPaths_ReturnsTrue(
            string path, bool expected)
        {
            CallIsAbsolutePath(path)
                .Should().Be(expected, because: $"'{path}' is a Unix absolute path");
        }
        /// <summary>
        /// TC25: Verifies that relative paths are not treated as absolute.
        /// </summary>
        [TestMethod]
        [DataRow("Photos/paris.jpg", false)]
        [DataRow("paris.jpg", false)]
        [DataRow("Photos\\image.png", false)]
        public void TC25_IsAbsolutePath_RelativePaths_ReturnsFalse(
            string path, bool expected)
        {
            CallIsAbsolutePath(path)
                .Should().Be(expected, because: $"'{path}' is a relative path and must be allowed");
        }
        /// <summary>
        // /// TC26: Verifies that attaching a duplicate photo returns an appropriate message and does not update storage.
        // /// </summary>
        [TestMethod]
        public async Task TC26_AttachPhoto_DuplicatePhoto_ReturnsAlreadyAttachedMessage()
        {
            // ARRANGE
            var asset = MakeAsset("Vapiano",
                photos: new List<string> { "Photos/vapiano.jpg" });

            _mockStorage
                .Setup(s => s.GetAssetsAsync())
                .ReturnsAsync(new List<Asset> { asset });
            // ACT
            var result = await _tool.AttachPhoto("Vapiano", "Photos/vapiano.jpg");
            // ASSERT
            result.Should().Contain("already attached",
                because: "duplicate photo must return the already-attached message");
            _mockStorage.Verify(
                s => s.UpdateAssetAsync(It.IsAny<Asset>()),
                Times.Never);
        }
        /// <summary>
        /// TC27: Verifies that absolute file paths are rejected for security reasons.
        /// </summary>
        [TestMethod]
        public async Task TC27_AttachPhoto_AbsolutePath_ReturnsValidationError()
        {
            // ACT
            var result = await _tool.AttachPhoto("Vapiano", @"C:\Photos\vapiano.jpg");
            // ASSERT
            result.Should().Contain("absolute path",
                because: "absolute Windows path must be rejected");
            _mockStorage.Verify(
                s => s.GetAssetsAsync(),
                Times.Never);
        }
        /// <summary>
        /// TC28: Verifies that a valid new photo is attached and persisted successfully.
        /// </summary>
        [TestMethod]
        public async Task TC28_AttachPhoto_NewValidPhoto_CallsUpdateAndReturnsDone()
        {
            // ARRANGE
            var asset = MakeAsset("Vapiano", photos: new List<string>());

            _mockStorage
                .Setup(s => s.GetAssetsAsync())
                .ReturnsAsync(new List<Asset> { asset });

            _mockStorage
                .Setup(s => s.UpdateAssetAsync(It.IsAny<Asset>()))
                .ReturnsAsync(true);

            // ACT
            var result = await _tool.AttachPhoto("Vapiano", "Photos/new_photo.jpg");

            // ASSERT
            result.Should().Contain("Done!",
                because: "new photo must return Done! confirmation");
            result.Should().Contain("Photos/new_photo.jpg");
            _mockStorage.Verify(
                s => s.UpdateAssetAsync(It.IsAny<Asset>()),
                Times.Once);
        }
        /// <summary>
        /// TC29: Verifies that an exact asset match returns full formatted details.
        /// </summary>
        [TestMethod]
        public async Task TC29_GetAssetDetails_ExactMatch_ReturnsFullFormattedBlock()
        {
            // ARRANGE
            var asset = MakeAsset(
                name: "Vapiano",
                category: "Restaurant",
                city: "Frankfurt",
                country: "Germany",
                region: "Hesse");

            _mockStorage
                .Setup(s => s.GetAssetsAsync())
                .ReturnsAsync(new List<Asset> { asset });

            // ACT
            var result = await _tool.GetAssetDetails("Vapiano");

            // ASSERT
            result.Should().Contain("Vapiano");
            result.Should().Contain("Restaurant");
            result.Should().Contain("Frankfurt");
            result.Should().Contain("Germany");
            result.Should().Contain("Hesse");
        }
        /// <summary>
        /// TC30: Verifies that a missing asset returns a not-found message with a save suggestion.
        /// </summary>
        [TestMethod]
        public async Task TC30_GetAssetDetails_NotFound_ReturnsNotFoundAndOfferToSave()
        {
            // ARRANGE
            _mockStorage
                .Setup(s => s.GetAssetsAsync())
                .ReturnsAsync(new List<Asset>());

            // ACT
            var result = await _tool.GetAssetDetails("Ghost Item");

            // ASSERT
            result.Should().Contain("do not know",
                because: "missing asset must return not-found phrase");
            result.Should().Contain("Would you like to save it now?",
                because: "not-found message must offer to save the item");
        }
    }
}