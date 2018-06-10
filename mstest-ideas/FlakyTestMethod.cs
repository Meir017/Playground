using System;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Playground.Tests
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class FlakyTestMethodAttribute : TestMethodAttribute
    {
        public int RetryCount { get; set; }

        public override TestResult[] Execute(ITestMethod testMethod)
        {
            TestResult result = testMethod.Invoke(new object[0]);
            var output = new StringBuilder();
            for (int i = 0; i < RetryCount; i++)
            {
                if (result.Outcome == UnitTestOutcome.Passed) break;
                output.AppendLine($"Run #{i + 1} completed with status {result.Outcome}. exception: {result.TestFailureException}");
                result = testMethod.Invoke(new object[0]);
            }
            if (output.Length > 0)
            {
                result.LogOutput += "[Failures]" + Environment.NewLine + output;
            }
            return new[] { result };
        }
    }

    [TestClass]
    public class TestClass
    {
        [FlakyTestMethod(RetryCount = 6)]
        public void FlakyTest()
        {
            Assert.IsTrue(new Random().NextDouble() > 0.8);
        }
    }
}