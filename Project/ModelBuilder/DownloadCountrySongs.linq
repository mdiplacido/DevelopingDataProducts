<Query Kind="Program">
  <Output>DataGrids</Output>
  <Reference>&lt;RuntimeDirectory&gt;\System.Net.Http.dll</Reference>
  <NuGetReference>HtmlAgilityPack</NuGetReference>
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>HtmlAgilityPack</Namespace>
</Query>

void Main()
{
	HttpClient client = GetCountryClient();
	var index1 = GetContent(client, "/viewall/topsongs.asp", "songlist").Result.Content;
	var index2 = GetContent(client, "/songs/browse.asp?l=K", "songlist2").Result.Content; 
	var index3 = GetContent(client, "/songs/browse.asp?l=L", "songlist3").Result.Content; 
	
	var sb = new StringBuilder(index1);
	sb.Append(index2);
	sb.Append(index3);
	
//	index.Dump();
	var songPaths = GetSongPaths(sb.ToString());
	
	List<Task<ContentContext>> work = new List<Task<ContentContext>>();
	
	foreach (var songPath in songPaths)
	{
		work.Add(GetContent(client, songPath, songPath));
	}
	
	var workArray = work.ToArray();
	workArray.Count().Dump();
	
	Task.WaitAll(workArray);
	
	NGramSequence[] sequences = work
//		.Take(1)
		.Select((x) => new { Identity = x.Result.Identity,  Chords = GetChords(x.Result.Content) })
		.SelectMany(x => GenerateNgramSequences.Generate(x.Chords))
		.GroupBy(x => x)
		.Select(x => new NGramSequence { 
			Sequence = x.Key, 
			SequenceCount = x.Count(),  
			SequenceGroup =  x.Key.Split(new [] { ',' }, StringSplitOptions.RemoveEmptyEntries).Count()
		})
		.OrderByDescending(x => x.SequenceCount)
		.ToArray();
//		.Dump();
		
	// compute counts per n-gram group
	var nGramCounts = sequences
		.GroupBy(x => x.SequenceGroup).Select(x => new { SequenceGroup = x.Key, Total = x.Select(y => y.SequenceCount).Sum() })
		.Dump();
		
	// join to n-gram group
	var sequenceAndNgramCounts = sequences.Join(
		nGramCounts, 
		outer => outer.SequenceGroup, 
		inner => inner.SequenceGroup,
		(outer, inner) => new { NgramSequence = outer, GroupInfo = inner }); 
		
	var sequenceAndNgramCountsUpdated = sequenceAndNgramCounts
		.Select(x => { x.NgramSequence.Probability = x.NgramSequence.SequenceCount / (double)x.GroupInfo.Total; return x; })
		.ToArray();
//		.Dump();
		
	var maxProbabilityPerGroup = sequenceAndNgramCountsUpdated
		.GroupBy(x => x.NgramSequence.SequenceGroup)
		.Select(x => new { SequenceGroup = x.Key, MaxProbability = x.Max(y => y.NgramSequence.Probability) })
		.Dump();
		
	var sequenceMap = sequenceAndNgramCountsUpdated.Join(
		maxProbabilityPerGroup, 
		outer => outer.NgramSequence.SequenceGroup, 
		inner => inner.SequenceGroup,
		(outer, inner) => { outer.NgramSequence.ProbabilityNullHypothesis = outer.NgramSequence.Probability / inner.MaxProbability; return outer; })
		.ToDictionary(x => x.NgramSequence.Sequence, y => y.NgramSequence)
		.Dump();
		
	// now we score using the map to get our song scores
	var scoredSongs = work
//		.Take(1)
		.Select((x) => new { Identity = x.Result.Identity, Chords = GetChords(x.Result.Content) })
		.Select(x => new { Identity = x.Identity, Chords = x.Chords, Score = CalculateScore(sequenceMap, GenerateNgramSequences.Generate(x.Chords)) })
		.Where(x => x.Chords.Any())
		.OrderByDescending(x => x.Score)
		.ToArray()
		.Dump();
		
	StringBuilder scores = new StringBuilder();
	scores.AppendLine("Score");
	scores.Append(string.Join(Environment.NewLine, scoredSongs.Select(x => x.Score.ToString("r"))));
		
	WriteCachedContent("Scores", scores.ToString());
	WriteCachedContent("Map", MapAsString(sequenceMap));
}

public static string MapAsString(IDictionary<string, NGramSequence> map)
{
	StringBuilder mapBlob = new StringBuilder();
	
	mapBlob.AppendLine("Sequence\tProbability\tProbabilityNullHypothesis\tProbabilityNullHypothesisNegativeLog\tSequenceCount\tSequenceGroup");
	
	foreach (var item in map)
	{
		mapBlob.Append(item.Key);
		mapBlob.Append("\t");
		mapBlob.Append(item.Value.Probability.ToString("r"));
		mapBlob.Append("\t");
		mapBlob.Append(item.Value.ProbabilityNullHypothesis.ToString("r"));
		mapBlob.Append("\t");
		mapBlob.Append(item.Value.ProbabilityNullHypothesisNegativeLog.ToString("r"));
		mapBlob.Append("\t");
		mapBlob.Append(item.Value.SequenceCount.ToString());
		mapBlob.Append("\t");
		mapBlob.Append(item.Value.SequenceGroup.ToString());
		mapBlob.Append(Environment.NewLine);
	}
	
	return mapBlob.ToString();
}

public static double CalculateScore(Dictionary<string, NGramSequence> map, IEnumerable<string> chordSequences)
{
	if (!chordSequences.Any())
	{
		return double.NegativeInfinity;
	}
	
	return chordSequences.Select(x => map[x].ProbabilityNullHypothesisNegativeLog).Average();
}

public static IEnumerable<string> GetChords(string song)
{
	HtmlDocument doc = new HtmlDocument();
 	doc.LoadHtml(song);
	return doc.DocumentNode
		.SelectNodes("//a")
		.Select(x => new { 
			Href = x.Attributes.Contains("href") ? x.Attributes["href"].Value : string.Empty,
			InnerText = x.InnerText,
			Class = x.Attributes.Contains("class") ? x.Attributes["class"].Value : string.Empty
		})
		.Where(x => x.Class == "chordlink")
		.Select(x => x.InnerText);
}

public static IEnumerable<string> GetSongPaths(string index)
{
	HtmlDocument doc = new HtmlDocument();
 	doc.LoadHtml(index);
	return doc.DocumentNode
		.SelectNodes("//a[@href]")
		.Select(x => x.Attributes["href"].Value)
		.Where(x => x.StartsWith("/tablature/"));
}

public static async Task<ContentContext> GetContent(HttpClient client, string path, string cacheKey)
{
	ContentContext result = new ContentContext() { Identity = path };
	
	string content;
	
	if (TryGetCachedContent(cacheKey, out content))
	{
//		Debug.Write(string.Format("using cached content for path: '{0}' and key: {1}", path, cacheKey));
		result.Content = content;
		return result;
	}
	
	var response = await client.GetAsync(path);
	
	result.Content = await response.Content.ReadAsStringAsync();
	
	await WriteCachedContentAsync(cacheKey, result.Content);
	
	return result;
}

public static void WriteCachedContent(string key, string content)
{
	string cacheFullPath = GetCacheFullPathName(key);
	File.WriteAllText(cacheFullPath, content);
}

public static async Task WriteCachedContentAsync(string key, string content)
{
	string cacheFullPath = GetCacheFullPathName(key);
    byte[] encodedText = Encoding.UTF8.GetBytes(content);
    using (FileStream sourceStream = new FileStream(cacheFullPath,
        FileMode.Create, FileAccess.Write, FileShare.None,
        bufferSize: 4096, useAsync: true))
    {
        await sourceStream.WriteAsync(encodedText, 0, encodedText.Length);
    };
}

public static bool TryGetCachedContent(string key, out string content)
{
	string cacheFullPath = GetCacheFullPathName(key);
	
	if (File.Exists(cacheFullPath))
	{
		content = File.ReadAllText(cacheFullPath);
		return true;
	}
	
	Debug.WriteLine("did not have cached content for cacheFullPath: " + cacheFullPath);
	
	content = null;
	return false;
}

public static string GetCacheFullPathName(string key)
{
	const string cacheBasePath = @"C:\temp\countrycache\";
	
	if (!Directory.Exists(cacheBasePath))
	{
		Directory.CreateDirectory(cacheBasePath);
	}
	
	key = key.Replace("/", "_").Replace("\\", "_").Replace(" ", "_").Replace("?", "_");
	
	string cacheFullPath = Path.Combine(cacheBasePath, key);
	return cacheFullPath;
}

public static HttpClient GetCountryClient()
{
	HttpClient client = new HttpClient();
	client.BaseAddress = new Uri("http://www.countrytabs.com/");
	return client;
}

// Define other methods and classes here
public static class GenerateNgramSequences
{
	public static IEnumerable<string> Generate(IEnumerable<string> chords)
	{
		const int nGramSize = 3;
		Queue<string> queue = new Queue<string>(nGramSize);
		
		foreach (var chord in chords)
		{
			queue.Enqueue(NormalizeChord(chord));
			
			yield return RenderSequence(queue);
			
			if (queue.Count == nGramSize)
			{
				queue.Dequeue();
			}
		}
	}
	
	private static string NormalizeChord(string chord)
	{
		return chord.Substring(0, 1).ToUpper() + chord.Substring(1);
	}
	
	public static string RenderSequence(Queue<string> queue)
	{
		return string.Join(",", queue.ToArray());
	}
}

public class NGramSequence
{
	public string Sequence { get; set; }
	public int SequenceCount { get; set; }
	public int SequenceGroup { get; set; }
	public double Probability { get; set; }
	public double ProbabilityNullHypothesis { get; set; }
	public double ProbabilityNullHypothesisNegativeLog 
	{ 
		get
		{
			return -Math.Log(this.ProbabilityNullHypothesis);
		}
	}
}

public class ContentContext
{
	public string Content { get; set; }
	public string Identity { get; set; }
}