using System.Text;
using Iris.Core;

namespace Iris.Services;

public sealed class RestrictedCommandParser : ICommandParser
{
    private static readonly HashSet<string> AllowedExecutables =
        new(StringComparer.OrdinalIgnoreCase) { "adb", "fastboot" };

    public CommandInvocation Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new FormatException("Enter an adb or fastboot command.");

        if (input.Length > 4096)
            throw new FormatException("The command is too long.");

        var tokens = Tokenize(input);
        if (tokens.Count == 0 || !AllowedExecutables.Contains(tokens[0]))
            throw new FormatException("Only adb and fastboot commands are allowed.");

        var arguments = tokens.Skip(1).ToArray();
        var forbiddenSelectors = new[] { "-s", "--serial", "-H", "-P", "-L", "-t", "-d", "-e", "--transport-id" };
        if (arguments.Any(argument => forbiddenSelectors.Any(selector =>
                argument.Equals(selector, StringComparison.OrdinalIgnoreCase) ||
                argument.StartsWith(selector + "=", StringComparison.OrdinalIgnoreCase))))
            throw new FormatException("Server, transport, and target selectors must be controlled by Iris.");
        return new CommandInvocation(tokens[0].ToLowerInvariant(), arguments);
    }

    private static List<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        var quote = '\0';

        for (var index = 0; index < input.Length; index++)
        {
            var character = input[index];
            if (character is '\r' or '\n' or '\0')
                throw new FormatException("Control characters are not allowed.");

            if (character is '&' or '|' or '>' or '<' or ';' or '`')
                throw new FormatException("Shell operators are not allowed.");

            if (character is '%' or '!')
                throw new FormatException("Environment expansion is not allowed.");

            if (character == '$' && index + 1 < input.Length && input[index + 1] == '(')
                throw new FormatException("Command substitution is not allowed.");

            if (character is '"' or '\'')
            {
                if (!quoted)
                {
                    quoted = true;
                    quote = character;
                    continue;
                }

                if (quote == character)
                {
                    quoted = false;
                    continue;
                }
            }

            if (!quoted && char.IsWhiteSpace(character))
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(character);
        }

        if (quoted)
            throw new FormatException("A quoted argument was not closed.");

        if (current.Length > 0)
            tokens.Add(current.ToString());

        return tokens;
    }
}
