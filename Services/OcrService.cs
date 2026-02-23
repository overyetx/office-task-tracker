using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace OfficeTaskTracker.Services;

/// <summary>
/// Represents a task extracted from OCR with name and price
/// </summary>
public class OcrTaskResult
{
    public string Name { get; set; } = string.Empty;
    public int Price { get; set; }
}

public class OcrResultContainer
{
    public List<OcrTaskResult> Tasks { get; set; } = new();
    public string ContractName { get; set; } = string.Empty;
    public string OfficeName { get; set; } = string.Empty;
}

public static class OcrService
{
    // Matches "TASK 1", "TASK 2", "TASK 1 $12000", etc.
    private static readonly Regex TaskHeaderPattern = new(@"^TASK\s*\d+(\s|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Matches price text like "$12000", "$12,000", "$12 000", "S12000" (OCR misread)
    private static readonly Regex PriceInLineRegex = new(@"[\$S]\s*(\d[\d\s,\.]*\d)", RegexOptions.Compiled);

    // Pure dollar line: "$12000", "$ 12 000", "S12000"
    private static readonly Regex PurePriceLineRegex = new(@"^[\$S]\s*[\d\s,\.]+$", RegexOptions.Compiled);

    private static readonly Regex PureNumberPattern = new(@"^\d+$", RegexOptions.Compiled);
    private static readonly Regex TimePattern = new(@"^\d{1,2}:\d{2}(:\d{2})?", RegexOptions.Compiled);
    private static readonly Regex FpsPattern = new(@"^\d+\s*fps$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Short UI words that should only match if the ENTIRE line equals them (exact match)
    private static readonly HashSet<string> UiExactMatchTexts = new(StringComparer.OrdinalIgnoreCase)
    {
        "BUY", "SELL", "CLOSE", "OPEN", "CANCEL", "OK", "YES", "NO",
        "ACCEPT", "DECLINE", "BACK", "NEXT", "MENU", "SETTINGS",
        "EXIT", "QUIT", "SAVE", "LOAD", "OPTIONS", "HELP", "COMPLETED",
        "NOTIFICATION", "STATE OFFICES"
    };

    // Longer UI phrases that should be matched as substrings within a line
    private static readonly HashSet<string> UiSubstringTexts = new(StringComparer.OrdinalIgnoreCase)
    {
        "BUY AN OFFICE", "OFFICE COSTS",
        "OFFICE ", "OFFICE OVERVIEW", "CONTRACT WITH", "Total profit",
        "You can take assignments from other offices",
        "with their interest rates", "agreement with the office"
    };

    public static async Task<OcrResultContainer> ExtractTasksFromBitmap(Bitmap bitmap)
    {
        var allLines = new List<string>();

        try
        {
            using var memoryStream = new MemoryStream();
            bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
            memoryStream.Position = 0;

            var randomAccessStream = new InMemoryRandomAccessStream();
            var outputStream = randomAccessStream.GetOutputStreamAt(0);
            var writer = new DataWriter(outputStream);
            writer.WriteBytes(memoryStream.ToArray());
            await writer.StoreAsync();
            await outputStream.FlushAsync();
            randomAccessStream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
            var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);

            var ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
            if (ocrEngine == null)
                ocrEngine = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"));

            if (ocrEngine != null)
            {
                var ocrResult = await ocrEngine.RecognizeAsync(softwareBitmap);
                foreach (var line in ocrResult.Lines)
                {
                    var text = line.Text.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        allLines.Add(text);
                        System.Diagnostics.Debug.WriteLine($"OCR LINE: {text}");
                    }
                }
            }

            softwareBitmap.Dispose();
            randomAccessStream.Dispose();
        }
        catch (Exception ex)
        {
            return new OcrResultContainer 
            { 
                Tasks = new List<OcrTaskResult> { new() { Name = $"[OCR Error: {ex.Message}]" } } 
            };
        }

        // Save raw OCR lines for debugging
        try
        {
            var debugPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OfficeTaskTracker", "ocr_debug.txt");
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(debugPath)!);
            File.WriteAllLines(debugPath, allLines.Prepend($"--- OCR {DateTime.Now:HH:mm:ss} ({allLines.Count} lines) ---"));
        }
        catch { }

        // 1. Try "CONTRACT WITH" for personal name/ID
        string contractName = string.Empty;
        var contractLine = allLines.FirstOrDefault(l => l.Contains("CONTRACT WITH", StringComparison.OrdinalIgnoreCase));
        if (contractLine != null)
        {
            contractName = contractLine.Replace("CONTRACT WITH", "", StringComparison.OrdinalIgnoreCase).Trim();
        }

        // 2. Try "OFFICE" for office name
        string officeName = string.Empty;
        var officeLine = allLines.FirstOrDefault(l => l.Contains("OFFICE ", StringComparison.OrdinalIgnoreCase) || 
                                                   l.Contains("OFFICE", StringComparison.OrdinalIgnoreCase));
        if (officeLine != null)
        {
            officeName = Regex.Replace(officeLine, @"^OFFICE\s*", "", RegexOptions.IgnoreCase).Trim();
        }

        var tasks = ExtractTasksWithPrices(allLines);
        return new OcrResultContainer 
        { 
            Tasks = tasks, 
            ContractName = contractName,
            OfficeName = officeName
        };
    }

    /// <summary>
    /// Extracts tasks with prices from OCR lines.
    /// Strategy:
    ///   1. Find all "Total profit $XXX" to compute exact unit price (Total / 6).
    ///   2. Find all "TASK N" headers and try to match them with adjacent description lines.
    ///   3. If some tasks are unmatched (grid layout causes headers and descriptions to be in separate groups),
    ///      collect remaining description candidates and assign them to unmatched tasks in order.
    /// </summary>
    private static List<OcrTaskResult> ExtractTasksWithPrices(List<string> allLines)
    {
        var taskNames = new Dictionary<int, string>();
        int detectedPrice = 0;
        int totalProfit = 0;

        // Step 1: Find Total Profit (e.g., "Total profit $72,000")
        foreach (var line in allLines)
        {
            if (line.Contains("Total profit", StringComparison.OrdinalIgnoreCase))
            {
                totalProfit = ExtractInlinePrice(line);
                if (totalProfit > 0)
                {
                    // Usually there are 6 tasks in a contract
                    detectedPrice = totalProfit / 6;
                    System.Diagnostics.Debug.WriteLine($"Found Total Profit: ${totalProfit:N0} -> Unit Price: ${detectedPrice:N0}");
                    break;
                }
            }
        }

        // Step 2: Collect all task header indices and try adjacent matching
        var taskHeaderIndices = new List<(int lineIndex, int taskNumber)>();
        for (int i = 0; i < allLines.Count; i++)
        {
            var line = allLines[i].Trim();
            if (!TaskHeaderPattern.IsMatch(line)) continue;
            
            var numberMatch = Regex.Match(line, @"\d+");
            if (!numberMatch.Success || !int.TryParse(numberMatch.Value, out int taskIndex)) continue;
            
            taskHeaderIndices.Add((i, taskIndex));
        }

        // Step 2a: Try to match task description from adjacent lines (original approach)
        var headerLineIndices = new HashSet<int>(taskHeaderIndices.Select(t => t.lineIndex));
        foreach (var (lineIndex, taskNumber) in taskHeaderIndices)
        {
            for (int j = lineIndex + 1; j < allLines.Count && j <= lineIndex + 4; j++)
            {
                var candidate = allLines[j].Trim();

                // Stop if we hit another TASK header
                if (TaskHeaderPattern.IsMatch(candidate)) break;
                if (IsPriceLine(candidate)) continue;
                if (candidate.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase)) continue;
                if (IsNonTaskLine(candidate)) continue;

                // Remove accidental inline prices or glitches like "52000"
                var cleaned = PriceInLineRegex.Replace(candidate, "").Trim();

                if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length >= 5)
                {
                    taskNames[taskNumber] = cleaned;
                    break;
                }
            }
        }

        // Step 2b: If some tasks are still unmatched (grid layout: headers grouped, descriptions grouped),
        // collect all candidate description lines that weren't matched and assign to unmatched tasks
        var unmatchedTaskNumbers = taskHeaderIndices
            .Select(t => t.taskNumber)
            .Where(n => !taskNames.ContainsKey(n))
            .OrderBy(n => n)
            .ToList();

        if (unmatchedTaskNumbers.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"Unmatched tasks after adjacent scan: {string.Join(", ", unmatchedTaskNumbers.Select(n => $"TASK {n}"))}");
            
            // Collect all lines that could be task descriptions but weren't already matched
            var matchedDescriptions = new HashSet<string>(taskNames.Values, StringComparer.OrdinalIgnoreCase);
            var candidateDescriptions = new List<string>();

            for (int i = 0; i < allLines.Count; i++)
            {
                if (headerLineIndices.Contains(i)) continue;

                var trimmed = allLines[i].Trim();
                if (IsNonTaskLine(trimmed)) continue;
                if (IsPriceLine(trimmed)) continue;
                if (trimmed.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase)) continue;

                var cleaned = PriceInLineRegex.Replace(trimmed, "").Trim();
                if (string.IsNullOrWhiteSpace(cleaned) || cleaned.Length < 5) continue;
                if (matchedDescriptions.Contains(cleaned)) continue;

                candidateDescriptions.Add(cleaned);
            }

            System.Diagnostics.Debug.WriteLine($"Available candidate descriptions: {candidateDescriptions.Count}");
            foreach (var desc in candidateDescriptions)
                System.Diagnostics.Debug.WriteLine($"  Candidate: {desc}");

            // Assign remaining candidates to unmatched tasks in order
            int assignIndex = 0;
            foreach (var taskNum in unmatchedTaskNumbers)
            {
                if (assignIndex >= candidateDescriptions.Count) break;
                taskNames[taskNum] = candidateDescriptions[assignIndex];
                System.Diagnostics.Debug.WriteLine($"Assigned '{candidateDescriptions[assignIndex]}' to TASK {taskNum}");
                assignIndex++;
            }
        }

        // Step 3: Fallback if Total Profit failed but we see repeated numbers like "52000" (which might be $12000 misread)
        if (detectedPrice == 0)
        {
            var potentialPrices = new List<int>();
            foreach (var line in allLines)
            {
                var p = ParsePossibleNumber(line);
                if (p > 1000 && p < 999999) potentialPrices.Add(p);
            }

            // Find the most frequent large number
            if (potentialPrices.Count > 0)
            {
                var mostFrequent = potentialPrices.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key;
                
                // OCR Glitch correction: If it reads "52000", it actually meant "$12000"
                if (mostFrequent.ToString().StartsWith("5") && mostFrequent >= 50000)
                {
                    string correctedStr = "1" + mostFrequent.ToString().Substring(1);
                    if (int.TryParse(correctedStr, out int corrected))
                        detectedPrice = corrected;
                }
                else
                {
                    detectedPrice = mostFrequent;
                }
                System.Diagnostics.Debug.WriteLine($"Fallback Price Detected: ${detectedPrice:N0} (from raw {mostFrequent})");
            }
        }

        System.Diagnostics.Debug.WriteLine($"FINAL PRICE: ${detectedPrice:N0} for {taskNames.Count} tasks");

        // Step 4: Build results
        var results = taskNames.OrderBy(kvp => kvp.Key).Select(kvp => new OcrTaskResult
        {
            Name = kvp.Value,
            Price = detectedPrice
        }).ToList();

        // Safety fallback if no tasks were matched via TASK N
        if (results.Count == 0)
        {
            foreach (var line in allLines)
            {
                var trimmed = line.Trim();
                if (!IsNonTaskLine(trimmed) && trimmed.Length >= 5 && !trimmed.StartsWith("TASK"))
                {
                    results.Add(new OcrTaskResult { Name = trimmed, Price = 0 });
                }
            }
        }

        return results;
    }

    /// <summary>Extract a purely numerical value from a string, aggressively.</summary>
    private static int ParsePossibleNumber(string text)
    {
        var match = Regex.Match(text, @"\b(\d+)\b");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int result))
            return result;
        return 0;
    }

    /// <summary>Is this price in a reasonable range for a task? ($1,000 - $999,999)</summary>
    private static bool IsReasonableTaskPrice(int price)
    {
        return price >= 1000 && price <= 999999;
    }

    /// <summary>Check if a line is purely a price</summary>
    private static bool IsPriceLine(string text)
    {
        return PurePriceLineRegex.IsMatch(text) || 
               (text.StartsWith('$') && text.Length <= 15) ||
               (text.StartsWith('S') && text.Length <= 8 && char.IsDigit(text[1]));
    }

    /// <summary>Extract a dollar amount from anywhere in a line</summary>
    private static int ExtractInlinePrice(string text)
    {
        var match = PriceInLineRegex.Match(text);
        if (!match.Success) return 0;

        var numStr = match.Groups[1].Value
            .Replace(",", "")
            .Replace(".", "")
            .Replace(" ", "")
            .Trim();

        return int.TryParse(numStr, out var price) ? price : 0;
    }

    /// <summary>Parse price from a pure price string</summary>
    public static int ParsePrice(string text)
    {
        return ExtractInlinePrice(text);
    }

    private static bool IsNonTaskLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (PurePriceLineRegex.IsMatch(text)) return true;
        if (text.StartsWith('$')) return true;
        if (TaskHeaderPattern.IsMatch(text)) return true;
        if (PureNumberPattern.IsMatch(text)) return true;
        if (TimePattern.IsMatch(text)) return true;
        if (FpsPattern.IsMatch(text)) return true;
        if (text.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase)) return true;
        // Exact match for short UI words (BUY, SELL, etc.)
        if (UiExactMatchTexts.Contains(text)) return true;
        // Substring match only for longer, unique UI phrases
        foreach (var uiText in UiSubstringTexts)
        {
            if (text.Contains(uiText, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        if (text.Length < 4) return true;
        return false;
    }

    // Legacy compat
    public static async Task<List<string>> ExtractTextFromBitmap(Bitmap bitmap)
    {
        var result = await ExtractTasksFromBitmap(bitmap);
        return result.Tasks.Select(t => t.Name).ToList();
    }
}
