using Xunit;

namespace Goat.Utility.Merlin.Lib.Tests
{
    public class GitIgnoreStyleMatcherTests
    {
        [Theory]
        [InlineData("project/src/File.cs", true)]
        [InlineData("project/src/SubDir/AnotherFile.cs", true)]
        [InlineData("project/src/File.txt", false)]
        [InlineData("project/obj/Debug/File.cs", false)]
        [InlineData("project/bin/Release/File.cs", true)]
        [InlineData("project/src/obj/TempFile.cs", false)]
        [InlineData("project/src/objFake/File.cs", true)]
        public void ShouldIncludeFile_WithCsInclusionAndObjExclusion(string filePath, bool expectedResult)
        {
            // Arrange
            var includePatterns = new List<string> { "*.cs" };
            var excludePatterns = new List<string> { "obj/" };

            // Act
            bool result = GitIgnoreStyleMatcher.ShouldIncludeFile(filePath, includePatterns, excludePatterns);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void ShouldIncludeFile_EmptyPatterns_IncludesAllFiles()
        {
            // Arrange
            var includePatterns = new List<string>();
            var excludePatterns = new List<string>();

            // Act & Assert
            Assert.True(GitIgnoreStyleMatcher.ShouldIncludeFile("file.txt", includePatterns, excludePatterns));
            Assert.True(GitIgnoreStyleMatcher.ShouldIncludeFile("src/file.cs", includePatterns, excludePatterns));
            Assert.True(GitIgnoreStyleMatcher.ShouldIncludeFile("obj/debug/file.dll", includePatterns, excludePatterns));
        }

        [Fact]
        public void ShouldIncludeFile_MultipleIncludePatterns()
        {
            // Arrange
            var includePatterns = new List<string> { "*.cs", "*.txt" };
            var excludePatterns = new List<string>();

            // Act & Assert
            Assert.True(GitIgnoreStyleMatcher.ShouldIncludeFile("file.cs", includePatterns, excludePatterns));
            Assert.True(GitIgnoreStyleMatcher.ShouldIncludeFile("file.txt", includePatterns, excludePatterns));
            Assert.False(GitIgnoreStyleMatcher.ShouldIncludeFile("file.js", includePatterns, excludePatterns));
        }

        [Fact]
        public void ShouldIncludeFile_MultipleExcludePatterns()
        {
            // Arrange
            var includePatterns = new List<string> { "*.cs" };
            var excludePatterns = new List<string> { "obj/", "bin/" };

            // Act & Assert
            Assert.True(GitIgnoreStyleMatcher.ShouldIncludeFile("src/file.cs", includePatterns, excludePatterns));
            Assert.False(GitIgnoreStyleMatcher.ShouldIncludeFile("obj/file.cs", includePatterns, excludePatterns));
            Assert.False(GitIgnoreStyleMatcher.ShouldIncludeFile("bin/debug/file.cs", includePatterns, excludePatterns));
        }

        [Fact]
        public void ShouldIncludeFile_CaseInsensitiveMatching()
        {
            // Arrange
            var includePatterns = new List<string> { "*.CS" };
            var excludePatterns = new List<string> { "OBJ/" };

            // Act & Assert
            Assert.True(GitIgnoreStyleMatcher.ShouldIncludeFile("file.cs", includePatterns, excludePatterns));
            Assert.False(GitIgnoreStyleMatcher.ShouldIncludeFile("obj/file.cs", includePatterns, excludePatterns));
        }
    }
}