using System.Text;
using System.Text.RegularExpressions;

namespace Goat.Utility.Merlin.Lib
{
    
    public class GitIgnoreStyleMatcher
    {
        public static bool ShouldIncludeFile(string file, List<string> includePatterns, List<string> excludePatterns)
        {
            var relativePath = file.Replace('\\', '/');
            var fileName = Path.GetFileName(file);
            bool isIncluded = includePatterns.Count == 0 || includePatterns.Any(p => MatchesPattern(relativePath, fileName, p));
            bool isExcluded = excludePatterns.Any(p => MatchesPattern(relativePath, fileName, p));
            return isIncluded && !isExcluded;
        }

        private static bool MatchesPattern(string relativePath, string fileName, string pattern)
        {
            pattern = pattern.Replace('\\', '/').Trim();
            bool isNegation = pattern.StartsWith("!");
            if (isNegation)
            {
                pattern = pattern.Substring(1);
            }

            string regex = ConvertGitIgnorePatternToRegex(pattern);
            bool isMatch = Regex.IsMatch(relativePath, regex, RegexOptions.IgnoreCase | RegexOptions.Singleline) ||
                           Regex.IsMatch(fileName, regex, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return isNegation ? !isMatch : isMatch;
        }

        public static string ConvertGitIgnorePatternToRegex(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return string.Empty;

            var regexBuilder = new StringBuilder();
            bool isEscaping = false;
            bool inCharacterSet = false;

            // If the pattern doesn't start with '/', it should match anywhere in the path
            if (!pattern.StartsWith("/"))
            {
                regexBuilder.Append("(?:^|.+/)");
            }
            else
            {
                regexBuilder.Append('^');
                pattern = pattern.Substring(1);
            }

            for (int i = 0; i < pattern.Length; i++)
            {
                char c = pattern[i];

                if (isEscaping)
                {
                    regexBuilder.Append(Regex.Escape(c.ToString()));
                    isEscaping = false;
                    continue;
                }

                if (c == '\\')
                {
                    isEscaping = true;
                    continue;
                }

                switch (c)
                {
                    case '*':
                        if (i + 1 < pattern.Length && pattern[i + 1] == '*')
                        {
                            regexBuilder.Append(".*");
                            i++;
                        }
                        else
                        {
                            regexBuilder.Append("[^/]*");
                        }
                        break;
                    case '?':
                        regexBuilder.Append("[^/]");
                        break;
                    case '[':
                        inCharacterSet = true;
                        regexBuilder.Append('[');
                        break;
                    case ']':
                        inCharacterSet = false;
                        regexBuilder.Append(']');
                        break;
                    case '/':
                        regexBuilder.Append('/');
                        break;
                    case '.':
                        regexBuilder.Append(Regex.Escape(c.ToString()));
                        break;
                    default:
                        if (inCharacterSet)
                            regexBuilder.Append(c);
                        else
                            regexBuilder.Append(Regex.Escape(c.ToString()));
                        break;
                }
            }

            // If the pattern doesn't end with '/', it should match files and directories
            if (!pattern.EndsWith("/"))
            {
                regexBuilder.Append("(?:$|/.*)");
            }
            else
            {
                regexBuilder.Append(".*$");
            }

            return regexBuilder.ToString();
        }
    }
}
