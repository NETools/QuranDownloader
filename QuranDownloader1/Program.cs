// - 1 -
using System.Text;
 
const int NumSurahs = 114;

HttpClient HttpClient = new HttpClient();
List<Task<byte[]>> RunningTasks = new();

Console.ForegroundColor = ConsoleColor.DarkGreen;
Console.WriteLine("bismi 'Llāhi 'r-rahmaani 'r-rahiimi");

Console.WriteLine();

for (int i = 1; i <= NumSurahs; i++)
{
    RunningTasks.Add(HttpClient.GetByteArrayAsync($"https://www.ewige-religion.info/koran/Koran/{GetLeadingZeroes(3, i)}.htm"));
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.WriteLine("[*] Adding Task");
}

while (RunningTasks.Count > 0)
{
    RunningTasks.Where(p => p.IsCompleted).ToList().ForEach(async p =>
    {
        Console.ForegroundColor = ConsoleColor.DarkGreen;
        Console.WriteLine("[*] Received data");
        var buffer = await p;
        var htmlBody = Encoding.GetEncoding("iso-8859-1").GetString(buffer);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("[*] Decoded.");

        var tokens = Tokenize(htmlBody);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("[*] Tokenized.");

        var rootNode = Html2DOM(tokens);

        Console.ForegroundColor = ConsoleColor.Cyan;

        var title = ParseHtmlSpecialChars(CollectMaximumDepth(rootNode, "title")[0].Content);
        Console.WriteLine($"[*] Parsed: {title}");

        var authors = CollectMaximumDepth(rootNode, "b");
        authors = authors.Skip(authors.FindIndex(p => p.Content.Equals("Versnr.")) + 1).ToList();

        var translations = CollectMaximumDepth(rootNode, "td");
        translations = translations.Skip(translations.FindIndex(p => p.Content.Equals("1"))).ToList();

        Dictionary<string, List<string>> extractedTranslation = new Dictionary<string, List<string>>();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("[*] Adding translations.");

        for (int i = 0; i < translations.Count; i += authors.Count + 1)
        {
            for (int j = 0; j < authors.Count; j++)
            {
                if (!extractedTranslation.ContainsKey(authors[j].Content))
                    extractedTranslation.Add(authors[j].Content, new List<string>());
                extractedTranslation[authors[j].Content].Add(translations[i + 1 + j].Content);
            }
        }
 
        foreach (var author in extractedTranslation)
        {
            if (!Directory.Exists(author.Key))
                Directory.CreateDirectory(author.Key);

            File.WriteAllLines(@$"{author.Key}\{title}.txt", author.Value);
        }

        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine("[*] Wrote to disk.");


        RunningTasks.Remove(p);
    });
}

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("[*] Done.");
Console.ReadLine();
string GetLeadingZeroes(int numZeroes, int index)
{
    return new string(Enumerable.Repeat<char>('0', numZeroes - ((int)(Math.Log10(index) + 1))).ToArray()) + index;
}

string ParseHtmlSpecialChars(string html)
{
    StringBuilder sb = new StringBuilder(1000);
    StringBuilder specialB = new StringBuilder();
    Dictionary<string, char> specialChars = new Dictionary<string, char>()
    {
        { "&auml;", 'ä' },
        { "&Auml;", 'Ä' },
        { "&ouml;", 'ö' },
        { "&Ouml;", 'Ö' },
        { "&uuml;", 'ü' },
        { "&Uuml;", 'Ü' },
        { "&szlig;", 'ß' }
    };

    bool specialFound = false;

    for (int i = 0; i < html.Length; i++)
    {
        var c = html[i];

        if (c == '&')
            specialFound = true;

        if (!specialFound)
            sb.Append(c);
        else if(c == ';')
        {
            specialB.Append(c);
            sb.Append(specialChars[specialB.ToString()]);
            specialFound = false;
            specialB.Clear();
        }
        else
        {
            specialB.Append(c);
        }
    }

    return sb.ToString();
}

IEnumerable<Token> Tokenize(string html)
{
    Dictionary<char, TokenType> TOKENS = new Dictionary<char, TokenType>()
{
    { '<',  TokenType.BracketOpen },
    { '>',  TokenType.BracketClose },
    { '/',  TokenType.Slash },
};
    StringBuilder charAccum = new StringBuilder();

    bool bracketOpened = false;
    int whiteSpaces = 0;

    for (int i = 0; i < html.Length; i++)
    {
        var c = html[i];

        if (TOKENS.ContainsKey(c))
        {
            if (charAccum.Length > 0)
            {
                yield return new Token()
                {
                    Data = charAccum.ToString(),
                    TokenType = TokenType.String
                };
                charAccum.Clear();
            }

            yield return new Token()
            {
                Data = c + "",
                TokenType = TOKENS[c]
            };

            if (c == '<')
                bracketOpened = true;
            else if (c == '>')
                bracketOpened = false;
        }
        else
        {
            if (bracketOpened && c == ' ')
            {
                if (whiteSpaces > 0)
                {
                    continue;
                }
                else whiteSpaces++;
            }else if (bracketOpened && c!=' ')
            {
                whiteSpaces = 0;
            }

            charAccum.Append(c);
        }

    }
}

List<HtmlNode> CollectMaximumDepth(HtmlNode node, string type)
{
    Stack<(HtmlNode Node, int Index)> nodeStack = new Stack<(HtmlNode, int)>();
    List<HtmlNode> nodeCollector = new List<HtmlNode>();

    HtmlNode current = node;

    int depth = 0;

    while (true)
    {
        if (current.Children.Count > 0)
        {
            if (nodeStack.Count - 1 < depth)
            {
                nodeStack.Push((current, 0));
                current = current.Children[0];
                depth++;
            }
            else
            {
                var last = nodeStack.Pop();

                if (last.Index + 1 >= last.Node.Children.Count)
                {
                    if (nodeStack.Count == 0)
                        break;
                    current = nodeStack.Peek().Node;
                    depth--;
                }
                else
                {
                    nodeStack.Push((last.Node, last.Index + 1));
                    current = last.Node.Children[nodeStack.Peek().Index];
                    depth++;
                }
            }
        }
        else if (current.Children.Count == 0)
        {
            if (current.Type.Equals(type))
                nodeCollector.Add(current);
            current = nodeStack.Peek().Node;
            depth--;
        }
    }
    return nodeCollector;
}

HtmlNode Html2DOM(IEnumerable<Token> tokens)
{
    HtmlNode root = new HtmlNode();
    Stack<HtmlNode> nodeStack = new Stack<HtmlNode>();

    var enumerator = tokens.GetEnumerator();
    while (enumerator.MoveNext())
    {
        var token = enumerator.Current;

        if (token.TokenType == TokenType.BracketOpen)
        {
            enumerator.MoveNext();
            HtmlNode currentNode = new();

            if(enumerator.Current.TokenType == TokenType.Slash)
            {
                nodeStack.Push(root);

                enumerator.MoveNext();
                var endingType = enumerator.Current.Data;

                var endingTag = nodeStack.Pop();
                List<HtmlNode> nodeAccumulator = new List<HtmlNode>();

                while (!endingTag.Type.Equals(endingType))
                {
                    nodeAccumulator.Add(endingTag);
                    endingTag = nodeStack.Pop();
                }

                foreach (var child in nodeAccumulator)
                {
                    endingTag.Children.Add(child);
                }

                root = nodeStack.Pop();
                root.Children.Add(endingTag);
            }
            else
            {
                nodeStack.Push(root);
                currentNode.PutAttribute(enumerator.Current.Data);
                root = currentNode;
            }
            continue;
        }

        if (token.TokenType == TokenType.BracketClose) continue;
        root.PutContent(token.Data);
    }
    return root;
}

enum TokenType
{
    BracketOpen,
    BracketClose,
    Slash,
    String
}

struct Token
{
    public TokenType TokenType { get; set; }
    public string? Data { get; set; }
}

class HtmlNode
{
    private StringBuilder _attributeBuilder = new StringBuilder();
    private StringBuilder _contentBuilder = new StringBuilder();

    public List<HtmlNode> Children { get; private set; } = new List<HtmlNode>();

    public string Type { get; private set; }
    public string Attribute => _attributeBuilder.ToString();
    public string Content => _contentBuilder.ToString();

    public void PutAttribute(string? data)
    {
        string[] args = data?.Split(' ');
        Type = args[0];

        _attributeBuilder.Append(string.Join(' ', args.Skip(1)));
    }

    public void PutContent(string? data)
    {
        _contentBuilder.Append(data);
    }

    public override string ToString()
    {
        return $"[{Type}] {Content}";
    }
}