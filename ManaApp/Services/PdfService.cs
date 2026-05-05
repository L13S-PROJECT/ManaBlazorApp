using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ManaApp.Services;

public class PdfService
{    
public class OrderItem
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
}

public class OrderHeader
{
    public string OrderNumber { get; set; } = "";
    public string Date { get; set; } = "";
    public string Customer { get; set; } = "";
}

public List<string> ExtractWords(byte[] pdfBytes)
{
    var result = new List<string>();

    using var stream = new MemoryStream(pdfBytes);
    using var document = UglyToad.PdfPig.PdfDocument.Open(stream);

    foreach (var page in document.GetPages())
    {
        foreach (var word in page.GetWords())
        {
            result.Add(word.Text);
        }
    }

    return result;
}

public OrderHeader ParseHeader(List<string> words)
{
    var header = new OrderHeader();

    for (int i = 0; i < words.Count; i++)
    {
        // Datums (6 cipari)
if (Regex.IsMatch(words[i], @"^\d{6}$") && string.IsNullOrEmpty(header.Date))
    {
        var raw = words[i]; // piem: 260225

        header.Date = $"20{raw.Substring(0,2)}-{raw.Substring(2,2)}-{raw.Substring(4,2)}";
    }

        // Order Nr (parasti 5 cipari pēc "Inköpsnr")

// Order Nr (pēc "Inköpsnr")
if (words[i].ToLower().Contains("inköpsnr"))
{
    for (int k = i + 1; k < i + 10 && k < words.Count; k++)
    {
        if (Regex.IsMatch(words[k], @"^\d{5}$"))
        {
            header.OrderNumber = words[k];
            break;
        }
    }
}

        // Customer (meklē "Leveransadress" un paņem nākamo 1–3 vārdus)
if (words[i].ToLower().Contains("leveransadress"))
{
    var customerParts = new List<string>();

    for (int j = i + 1; j < words.Count; j++)
    {
        var w = words[j].ToLower();

        // stop pie nākamā bloka (arī ja salipis teksts)
        if (w.Contains("leverantör"))
            break;

        customerParts.Add(words[j]);
    }

    header.Customer = string.Join(" ", customerParts).Trim();
}
    }

    return header;
}

public OrderHeader ParseHeaderFromCoordinates(List<WordPosition> words)
{
    var header = new OrderHeader();

    // Order Nr
    var orderWord = words.FirstOrDefault(w =>
        w.Text.ToLower().Contains("inköpsnr"));

    if (orderWord != null)
    {
        var orderNr = words
            .Where(w =>
                Math.Abs(w.Y - orderWord.Y) < 3 && // tolerance!
                w.X > orderWord.X)
            .OrderBy(w => w.X)
            .FirstOrDefault(w => Regex.IsMatch(w.Text, @"^\d{5}$"));

        if (orderNr != null)
            header.OrderNumber = orderNr.Text;
    }

    // Date
    var dateWord = words.FirstOrDefault(w =>
        Regex.IsMatch(w.Text, @"^\d{6}$"));

    if (dateWord != null)
    {
        var raw = dateWord.Text;
        header.Date = $"20{raw.Substring(0,2)}-{raw.Substring(2,2)}-{raw.Substring(4,2)}";
    }

    // Customer (no Leveransadress kolonnas)
    var marker = words.FirstOrDefault(w =>
        w.Text.ToLower().Contains("leveransadress"));

    if (marker != null)
    {
        var customerWords = words
            .Where(w =>
                w.Y < marker.Y &&
                Math.Abs(w.X - marker.X) < 200)
            .OrderByDescending(w => w.Y)
            .ThenBy(w => w.X)
            .Take(3) // pietiek lai dabūtu "SIT RIGHT AB"
            .Select(w => w.Text);

        header.Customer = string.Join(" ", customerWords);
    }

    return header;
}

public class OrderResult
{
    public OrderHeader Header { get; set; } = new();
    public List<OrderItem> Items { get; set; } = new();
}

public OrderResult ParseDocument(byte[] pdfBytes)
{
    var words = ExtractWords(pdfBytes);
    var wordPositions = ExtractWordPositions(pdfBytes);
    var lines = GroupWordsIntoLines(wordPositions);

    var headerFromWords = ParseHeader(words);
    var headerFromCoords = ParseHeaderFromCoordinates(wordPositions);

    return new OrderResult
    {
        Header = new OrderHeader
        {
            OrderNumber = headerFromWords.OrderNumber, // ← no words
            Date = headerFromWords.Date,               // ← no words
            Customer = headerFromCoords.Customer       // ← no coordinates
        },
        Items = ParseProductsFromLinesCoordinates(lines)
    };
}

public class WordPosition
{
    public string Text { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public int Page { get; set; }
}

public List<WordPosition> ExtractWordPositions(byte[] pdfBytes)
{
    var result = new List<WordPosition>();

    using var stream = new MemoryStream(pdfBytes);
    using var document = UglyToad.PdfPig.PdfDocument.Open(stream);

    int pageNumber = 0;

foreach (var page in document.GetPages())
{
    pageNumber++;

    foreach (var word in page.GetWords())
    {
        result.Add(new WordPosition
        {
            Text = word.Text,
            X = word.BoundingBox.Left,
            Y = word.BoundingBox.Bottom,
            Page = pageNumber
        });
    }
}

    return result;
}

public List<List<WordPosition>> GroupWordsIntoLines(List<WordPosition> words)
{
    var lines = new List<List<WordPosition>>();

    var ordered = words
        .OrderByDescending(w => w.Y) // no augšas uz leju
        .ToList();

    const double tolerance = 3; // cik tuvu Y skaitām par vienu rindu

    foreach (var word in ordered)
    {
        var line = lines.FirstOrDefault(l => Math.Abs(l[0].Y - word.Y) < tolerance);

        if (line == null)
        {
            lines.Add(new List<WordPosition> { word });
        }
        else
        {
            line.Add(word);
        }
    }

    // sakārto katru rindu no kreisās uz labo
    foreach (var line in lines)
    {
        line.Sort((a, b) => a.X.CompareTo(b.X));
    }

    return lines;
}

public List<OrderItem> ParseProductsFromLinesCoordinates(List<List<WordPosition>> lines)
{
    var result = new List<OrderItem>();

    foreach (var line in lines)
    {
        // atrodam unit (St / Sats)
        var unitWord = line.FirstOrDefault(w =>
            w.Text == "St" || w.Text == "Sats");

        if (unitWord == null)
            continue;

        // atrodam quantity (skaitlis tieši pirms unit)
        var qtyWord = line
            .Where(w => w.X < unitWord.X)
            .OrderByDescending(w => w.X)
            .FirstOrDefault(w => int.TryParse(w.Text, out _));

        if (qtyWord == null)
            continue;

        if (!int.TryParse(qtyWord.Text, out int qty))
            continue;

        // atrodam code (pirmais garais kods rindā)
        var codeWord = line.FirstOrDefault(w =>
            Regex.IsMatch(w.Text, @"^\d{5,}[A-Z0-9\/]*$"));

        if (codeWord == null)
            continue;

        // name = viss starp code un qty (ignorējam 2609 tipa kolonnas)
        var nameParts = line
            .Where(w => w.X > codeWord.X && w.X < qtyWord.X)
            .Select(w => w.Text)
            .Where(t => !Regex.IsMatch(t, @"^\d{4}$")); // filtrē 2609 tipa laukus

        var name = string.Join(" ", nameParts);

        if (string.IsNullOrWhiteSpace(name))
            continue;

        result.Add(new OrderItem
        {
            Code = codeWord.Text,
            Name = name,
            Quantity = qty
        });
    }

    return result;
}

}