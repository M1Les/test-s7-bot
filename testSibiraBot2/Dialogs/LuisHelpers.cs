using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using BingMapsRESTToolkit;
using Microsoft.Bot.Builder.Luis.Models;
using Microsoft.Bot.Connector;
using nvk.Helpers;
using testSibiraBot2.MicrosoftTranslatorService;

public static class LuisHelpers {
    public static IEnumerable<EntityRecommendation> Scored(this IEnumerable<EntityRecommendation> source, Double score = 0.9)
    {
        return source?.Where(e => (e?.Score >= score) || ((null != e) && (null == e.Score)));
    }

    public static IEnumerable<EntityRecommendation> ByType(this IEnumerable<EntityRecommendation> source, String type)
    {
        return source.GetChildEntiesByType(null, type, TypeCheckerFull);
    }

    public static IEnumerable<EntityRecommendation> BySubType(this IEnumerable<EntityRecommendation> source, String type)
    {
        return source.GetChildEntiesByType(null, type, TypeCheckerSubType);
    }

    public static IEnumerable<EntityRecommendation> ChildrenByType(this IEnumerable<EntityRecommendation> source, EntityRecommendation parent, String type)
    {
        return source.GetChildEntiesByType(parent, type, TypeCheckerFull);
    }

    public delegate bool TypeChecker(String type1, String type2);

    private static readonly TypeChecker TypeCheckerFull =
        (t1, t2) => String.Equals(t1, t2, StringComparison.InvariantCultureIgnoreCase);
    private static readonly TypeChecker TypeCheckerSubType =
        (t1, t2) => (t1 ?? String.Empty).StartsWith(t2 ?? String.Empty, StringComparison.InvariantCultureIgnoreCase);

    public static IEnumerable<EntityRecommendation> GetChildEntiesByType(this IEnumerable<EntityRecommendation> source, EntityRecommendation parent, String type, TypeChecker typeChecker)
    {
        typeChecker = typeChecker ?? TypeCheckerFull;

        return source?
            .Where(e => (null == parent) || ((parent.StartIndex <= e.StartIndex) && (e.EndIndex <= parent.EndIndex)))
            .Where(e => typeChecker(e.Type, type));
    }

    public static EntityRecommendation[] ScoreEntitiesGeography(this LuisResult lr)
    {
        return (lr?.Entities?.BySubType("builtin.geography")?.Scored(0.5) ?? new EntityRecommendation[0]).ToArray();
    }

    public static EntityRecommendation[] ScoreEntities(this LuisResult lr)
    {
        return (lr?.Entities?.Scored() ?? new EntityRecommendation[0]).ToArray();
    }

    private const String ConfidenceDefault = "High";

    public const String BuiltInGeographyCity = "builtin.geography.city";
    public const String BuiltInGeographyCountry = "builtin.geography.country";
    
    private const String BingMapsKeyMsTeam = "AngIydqdFB0kbCLQDr3vVqPbHiDOLYvCBNreIYwxtCoekUKBpuKwTjUsHcmzg3jk";

    async public static Task<(String normalized, EntityRecommendation entity)> NormalizeGeography(this EntityRecommendation source, 
        String confidence = ConfidenceDefault, bool cityFullName = true)
    {
        var resultNone = (null as String, source);

        if (null == source) { return resultNone; }
        confidence = confidence ?? ConfidenceDefault;

        var isCity = BuiltInGeographyCity.Equals(source.Type, StringComparison.InvariantCultureIgnoreCase);
        var isCountry = BuiltInGeographyCountry.Equals(source.Type, StringComparison.InvariantCultureIgnoreCase);
        if ((false == isCity) && (false == isCountry)) { return resultNone; }

        // ((BingMapsRESTToolkit.Location)(r.ResourceSets[0x00000000].Resources[0x00000000])).Address.CountryRegion
        // ((BingMapsRESTToolkit.Location)(r.ResourceSets[0x00000000].Resources[0x00000000])).Confidence
        // r.StatusCode
        // r.errorDetails

        // ReSharper disable once ReplaceWithSingleCallToFirstOrDefault
        var normalized = new[]
            {
                await ServiceManager.GetResponseAsync(new GeocodeRequest()
                {
                    BingMapsKey = BingMapsKeyMsTeam, Culture = "ru-ru", Query = source.Entity
                })
            }.Where(resp => 200 == resp.StatusCode)
                
            .SelectMany(resp => resp.ResourceSets).SelectMany(rs => rs.Resources)
            .OfType<Location>()
                
            .Where(res => confidence.Equals(res.Confidence, StringComparison.InvariantCultureIgnoreCase))
            //.Where(res => "CountryRegion".Equals(res.EntityType, StringComparison.InvariantCultureIgnoreCase))
                
            .Select(res => isCountry ? 
                res.Address?.CountryRegion : 
                ((true == cityFullName) ? $"{res.Address?.Locality}, {res.Address?.CountryRegion}" : $"{res.Address?.Locality}"))
            .Where(name => false == String.IsNullOrEmpty(name)).FirstOrDefault();

        return (normalized, source);
    }

    public static (String normalized, EntityRecommendation entity)? GetGeographyNextToEntity(this IEnumerable<EntityRecommendation> entities, 
        String type, IEnumerable<(String normalized, EntityRecommendation entity)> nGeography)
    {
        var e = entities.ByType(type).FirstOrDefault();
        return (null != e) ? (nGeography?.Where(n => n.entity.StartIndex == (e.EndIndex + 2)).FirstOrDefault()) : null;
    }

    private static readonly Dictionary<String, (DateTimeOffset mark, Task<String> token, TimeSpan lifetime)> _tokens =
        new Dictionary<String, (DateTimeOffset mark, Task<String> token, TimeSpan lifetime)>();

    private static readonly Object _lockToken = new Object();

    private static readonly TimeSpan TokenLifeTimeDefault = new TimeSpan(hours:0, minutes: 5, seconds: 0);

    async private static Task<String> GetAzureCognitiveAccessToken(String key)
    {
        var result = null as Task<String>;

        async Task<String> PostIssueTokenRequest()
        {
            using (var http = new HttpClient())
            {
                return "Bearer " +
                       await http.PostAsync(
                               $"https://api.cognitive.microsoft.com/sts/v1.0/issueToken?Subscription-Key={key}",
                               new StringContent(String.Empty))
                           .Await(r => r.EnsureSuccessStatusCode().Content.ReadAsStringAsync());
            }
        };
        
        lock (_lockToken)
        {
            var now = DateTimeOffset.UtcNow;
            var (isFound, rec) = _tokens.Find(key);
            
            if ((false == isFound) || ((now - rec.mark) > rec.lifetime))
            {
                result = PostIssueTokenRequest();
                _tokens[key] = (mark: now, token: result, lifetime: TokenLifeTimeDefault);
            }
            else
            {
                result = rec.token;
            }
        }
        
        return await result;
    }
    
    private const String TranslateSubscriptionKeyMsTeam = @"5308420aa9584eaeb1570b2f00bdee7e";
    async public static Task<String> Translate(this String message)
    {
        if (null == message) { message = String.Empty; }

        var result = String.Empty;

        var token = await GetAzureCognitiveAccessToken(TranslateSubscriptionKeyMsTeam); 
        try
        {
            // workaround for issue with OperationContextScope\OperationContext async support in 4.6.2

            using (var tr = new LanguageServiceClient())
            {
                var mprop = new HttpRequestMessageProperty {Method = "POST"};
                mprop.Headers.Add("Authorization", token);

                var ocxOld = OperationContext.Current;
                try
                {
                    var ocx = new OperationContext(tr.InnerChannel);
                    OperationContext.Current = ocx;

                    ocx.OutgoingMessageProperties[HttpRequestMessageProperty.Name] = mprop;

                    var textOnEnglish = await tr.TranslateAsync(String.Empty, message, String.Empty, "en", @"text/plain",
                        String.Empty,
                        String.Empty);
                    result = textOnEnglish;
                }
                finally { OperationContext.Current = ocxOld; }
            }
        }
        catch (Exception e)
        {
            // TODO: exception processing
            Debug.WriteLine(e);
        }
            
        return result;
    }

    async public static Task<(Boolean isProcessed, (String normalized, EntityRecommendation entity)? geo)> ProcessLocation(EntityRecommendation[] entities)
    {
        do
        {
            if (1 != entities?.LongLength) { break; }

            var n = await entities.ToArray().First().NormalizeGeography();
            if (null == n.normalized) { break; }

            return (true, n);
        } while (false);

        return (false, null);
    }
}