using Microsoft.Extensions.AI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReminderAgent.Domain;
using ReminderAgent.Infrastructure;
using ReminderAgent.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace UnitTest_SprintSix
{
    /// <summary>
    /// Contains tests to verify that service implementations correctly follow
    /// their respective interface contracts.
    /// </summary>
    [TestClass]
    public class InterfaceContractTests
    {
        /// <summary>
        /// Verifies that SimilarityService implements ISimilarityService.
        /// </summary>
        //ISimilarityService 

        [TestMethod]
        public void SimilarityService_Implements_ISimilarityService()
        {
            var sut = new SimilarityService();

            Assert.IsInstanceOfType(sut, typeof(ISimilarityService),
                "SimilarityService must implement ISimilarityService");
        }
        /// <summary>
        /// Verifies that ISimilarityService declares the CosineSimilarity method.
        /// </summary>
        [TestMethod]
        public void ISimilarityService_HasMethod_CosineSimilarity()
        {
            var method = typeof(ISimilarityService).GetMethod("CosineSimilarity");

            Assert.IsNotNull(method,
                "ISimilarityService must declare CosineSimilarity");
        }
        /// <summary>
        /// Verifies that CosineSimilarity returns a float value.
        /// </summary>
        [TestMethod]
        public void ISimilarityService_CosineSimilarity_ReturnType_IsFloat()
        {
            var method = typeof(ISimilarityService).GetMethod("CosineSimilarity");

            Assert.AreEqual(typeof(float), method!.ReturnType,
                "CosineSimilarity must return float");
        }
        /// <summary>
        /// Verifies that CosineSimilarity accepts two float array parameters.
        /// </summary>
        [TestMethod]
        public void ISimilarityService_CosineSimilarity_TwoFloatArrayParams()
        {
            var method     = typeof(ISimilarityService).GetMethod("CosineSimilarity");
            var parameters = method!.GetParameters();

            Assert.AreEqual(2, parameters.Length);
            Assert.AreEqual(typeof(float[]), parameters[0].ParameterType);
            Assert.AreEqual(typeof(float[]), parameters[1].ParameterType);
        }
        /// <summary>
        /// Verifies that ISimilarityService declares GetTopKSimilarAsync method.
        /// </summary>
        [TestMethod]
        public void ISimilarityService_HasMethod_GetTopKSimilarAsync()
        {
            var method = typeof(ISimilarityService).GetMethod("GetTopKSimilarAsync");

            Assert.IsNotNull(method,
                "ISimilarityService must declare GetTopKSimilarAsync");
        }
        /// <summary>
        /// Verifies that GetTopKSimilarAsync returns Task of List of Asset.
        /// </summary>
        [TestMethod]
        public void ISimilarityService_GetTopKSimilarAsync_ReturnType_IsTaskOfListAsset()
        {
            var method = typeof(ISimilarityService).GetMethod("GetTopKSimilarAsync");

            Assert.AreEqual(typeof(Task<List<Asset>>), method!.ReturnType,
                "GetTopKSimilarAsync must return Task<List<Asset>>");
        }

        /// <summary>
        // /// Verifies that EmbeddingService implements IEmbeddingService.
        // /// </summary>

        [TestMethod]
        public void EmbeddingService_Implements_IEmbeddingService()
        {
            var fake = new FakeEmbeddingGenerator();
            var sut  = new EmbeddingService(fake);

            Assert.IsInstanceOfType(sut, typeof(IEmbeddingService),
                "EmbeddingService must implement IEmbeddingService");
        }
        /// <summary>
        /// Verifies that IEmbeddingService declares GenerateEmbeddingAsync method.
        /// </summary>
        [TestMethod]
        public void IEmbeddingService_HasMethod_GenerateEmbeddingAsync()
        {
            var method = typeof(IEmbeddingService).GetMethod("GenerateEmbeddingAsync");

            Assert.IsNotNull(method,
                "IEmbeddingService must declare GenerateEmbeddingAsync");
        }
        /// <summary>
        /// Verifies that GenerateEmbeddingAsync includes required parameters.
        /// </summary>

        [TestMethod]
        public void IEmbeddingService_GenerateEmbeddingAsync_HasRequiredParams()
        {
            var method     = typeof(IEmbeddingService).GetMethod("GenerateEmbeddingAsync");
            var paramNames = method!.GetParameters().Select(p => p.Name).ToArray();

            CollectionAssert.Contains(paramNames, "name",           "Must have 'name' parameter");
            CollectionAssert.Contains(paramNames, "category",       "Must have 'category' parameter");
            CollectionAssert.Contains(paramNames, "userExperience", "Must have 'userExperience' parameter");
        }
        /// <summary>
        /// Verifies that GenerateEmbeddingAsync includes optional parameters.
        /// </summary>
        [TestMethod]
        public void IEmbeddingService_GenerateEmbeddingAsync_HasOptionalParams()
        {
            var method         = typeof(IEmbeddingService).GetMethod("GenerateEmbeddingAsync");
            var optionalParams = method!.GetParameters()
                .Where(p => p.HasDefaultValue)
                .Select(p => p.Name)
                .ToArray();

            CollectionAssert.Contains(optionalParams, "userInput", "'userInput' must be optional");
            CollectionAssert.Contains(optionalParams, "tags",      "'tags' must be optional");
        }
        /// <summary>
        /// Verifies that GenerateEmbeddingAsync returns Task of float array.
        /// </summary>
        [TestMethod]
        public void IEmbeddingService_GenerateEmbeddingAsync_ReturnType_IsTaskOfFloatArray()
        {
            var method = typeof(IEmbeddingService).GetMethod("GenerateEmbeddingAsync");

            Assert.AreEqual(typeof(Task<float[]>), method!.ReturnType,
                "GenerateEmbeddingAsync must return Task<float[]>");
        }
        /// <summary>
        /// Verifies that IEmbeddingService declares GenerateQueryEmbeddingAsync method.
        /// </summary>
        [TestMethod]
        public void IEmbeddingService_HasMethod_GenerateQueryEmbeddingAsync()
        {
            var method = typeof(IEmbeddingService).GetMethod("GenerateQueryEmbeddingAsync");

            Assert.IsNotNull(method,
                "IEmbeddingService must declare GenerateQueryEmbeddingAsync");
        }
        /// <summary>
        /// Verifies that GenerateQueryEmbeddingAsync accepts a single string parameter.
        /// </summary>
        [TestMethod]
        public void IEmbeddingService_GenerateQueryEmbeddingAsync_SingleStringParam()
        {
            var method     = typeof(IEmbeddingService).GetMethod("GenerateQueryEmbeddingAsync");
            var parameters = method!.GetParameters();

            Assert.AreEqual(1, parameters.Length,
                "GenerateQueryEmbeddingAsync must accept exactly one parameter");
            Assert.AreEqual(typeof(string), parameters[0].ParameterType,
                "The parameter must be of type string");
        }
        /// <summary>
        /// Verifies that GenerateQueryEmbeddingAsync returns Task of float array.
        /// </summary>

        [TestMethod]
        public void IEmbeddingService_GenerateQueryEmbeddingAsync_ReturnType_IsTaskOfFloatArray()
        {
            var method = typeof(IEmbeddingService).GetMethod("GenerateQueryEmbeddingAsync");

            Assert.AreEqual(typeof(Task<float[]>), method!.ReturnType,
                "GenerateQueryEmbeddingAsync must return Task<float[]>");
        }
    }
}
