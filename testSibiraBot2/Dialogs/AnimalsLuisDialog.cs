using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using BingMapsRESTToolkit;
using Chronic.Tags.Repeaters;
using Microsoft.Bot.Builder.Dialogs.Internals;
using nvk.Helpers;
using testSibiraBot2.Dialogs;
using testSibiraBot2.MicrosoftTranslatorService;

namespace testSibiraBot2.Dialogs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Web;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Builder.FormFlow;
    using Microsoft.Bot.Builder.Luis;
    using Microsoft.Bot.Builder.Luis.Models;
    using Microsoft.Bot.Connector;

    //[LuisModel("33e15373-7746-4975-8347-2ec9d2f930d8", "268cb802b893461591e26a07c2dac50f")]
    //[LuisModel("13ba4fe3-37d5-4cf9-a106-e746a641b2c5", "268cb802b893461591e26a07c2dac50f", staging: true)]
    //[LuisModel("77ee5d67-00df-4f2a-8c9f-8db37e3011b6", "e5f0c512f3c44c37830031ff4a6378f2")]
    [LuisModel("77ee5d67-00df-4f2a-8c9f-8db37e3011b6", "e5f0c512f3c44c37830031ff4a6378f2")]
    [Serializable]
    public class AnimalsLuisDialog : SibiraLuisDialog<Object>
    {
        [NonSerialized]
        private readonly LuisResult _start;

        public AnimalsLuisDialog(LuisResult start, Boolean isDebug = false, params ILuisService[] services) : base(isDebug, services)
        {
            _start = start;
        }

        private M.Model _model { get; set; } = new M.Model();
        private StateMachine _state { get; set; } = StateMachine.Initial;

        public Random _rnd { get; } = new Random();

        async public override Task StartAsync(IDialogContext context)
        {
            //Microsoft.Bot.Builder.Dialogs.Extensions
            //    .Wait((IDialogStack) context, new ResumeAfter<IMessageActivity>(this.MessageReceived));

            await NextAction(context, null, _start);
        }

        async protected override Task NextAction(IDialogContext context, IAwaitable<IMessageActivity> message, LuisResult result)
        {
            System.Diagnostics.Trace.TraceInformation($"Luis query: {result.Query}. Entities: [{string.Join(",",result.Entities.Select(e=> $"{{{e.Type}, {e.Entity}}}").ToArray())}]");
            switch (_state)
            {
                case StateMachine.Initial:
                    _state = StateMachine.InitialResponse;
                    
                    _model= new M.Model();
                    
                    goto case StateMachine.InitialResponse;
                
                case StateMachine.InitialResponse:
                    if (I.AnimalTransportation != result.TopScoringIntent.Intent)
                    {
                        await context.PostAsync($"Извините, но я не могу понять Ваш вопрос.");
                        context.Done(null as Object);
                        return;
                    }

                    await IntentAnimalTransportation(context, result);
                    goto case StateMachine.Weight;
                    
                case StateMachine.Weight:
                    if (true == _model.IsWeightValid) { goto case StateMachine.Dimension; }
                    
                    _state = StateMachine.WeightResponse;
                    
                    await context.PostAsync($"Укажите вес животного:");
                    //PromptDialog.Number(context, this.PromptResponseWeight, "Укажите вес животного в килограммах");
                   
                    //goto case StateMachine.Weight;

                    context.Wait(this.ResponseMessageReceived);
                    break;

                case StateMachine.WeightResponse:
                    //if (I.Weight != result.TopScoringIntent.Intent) { await CantUnderstand(context); }
                    //else {
                        await IntentWeight(context, result);
                    //}
                    
                    goto case StateMachine.Weight;

                case StateMachine.Dimension:
                    if (true == _model.IsDimensionValid) { goto case StateMachine.TravelType; }
                    
                    _state = StateMachine.DimensionResponse;
                    
                    await context.PostAsync($"Укажите размеры клетки (длина, ширина и высота в сантиметрах):");
                    context.Wait(this.ResponseTranslateWhenIntent);
                    break;

                case StateMachine.DimensionResponse:
                    //if (I.Dimension != result.TopScoringIntent.Intent) { await CantUnderstand(context); }
                    //else {
                        await IntentDimension(context, result);
                    //}
                    
                    goto case StateMachine.Dimension;

                case StateMachine.TravelType:
                    if (true == _model.IsTravelTypeValid) { goto case StateMachine.LocationFrom; }
                    
                    _state = StateMachine.TravelTypeResponse;
                    
                    {
                        var m = context.MakeMessage();
                        m.Attachments.Add(
                            new ThumbnailCard(text: $"Где Вы желаете везти своё животное ?", 
                                buttons: new []
                                {
                                    new CardAction(ActionTypes.ImBack, title: TravelTypeCabin, value: TravelTypeCabin),
                                    new CardAction(ActionTypes.ImBack, title: TravelTypeCheckedIn, value: TravelTypeCheckedIn),
                                }).ToAttachment());
                        await context.PostAsync(m);
                    }
                    context.Wait(this.ResponseMessageReceived);
                    break;

                case StateMachine.TravelTypeResponse:
                    {
                        var m = context.Activity?.AsMessageActivity()?.Text;
                        if (TravelTypeCabin.Equals(m, StringComparison.InvariantCultureIgnoreCase)) {
                            _model.TravelType = M.TravelType.Cabin;
                        }
                        else if (TravelTypeCheckedIn.Equals(m, StringComparison.InvariantCultureIgnoreCase))
                        {
                            _model.TravelType = M.TravelType.CheckedIn;
                        }
                        else
                        {
                            await CantUnderstand(context);
                        }
                    }
                    
                    goto case StateMachine.TravelType;

                case StateMachine.LocationFrom:
                    if (true == _model.IsLocationFromValid) { goto case StateMachine.LocationTo; }
                    
                    _state = StateMachine.LocationFromResponse;
                    
                    await context.PostAsync($"Откуда Вы вылетаете ?");
                    context.Wait(this.ResponseTranslateWhenIntent);
                    break;

                case StateMachine.LocationFromResponse:
                    {
                        //var n = await LuisHelpers.ProcessLocation(result.ScoreEntitiesGeography());
                        //if (true == n.isProcessed) { _model.LocationFrom = n.geo?.normalized; }
                        await ProcessGeoFromPredictive(new EntityRecommendation[] { new EntityRecommendation("PlaceName", entity: result.Query) }, _model);
                        if (!_model.IsLocationFromValid) { await CantUnderstand(context); }
                    }

                    goto case StateMachine.LocationFrom;
                    
                case StateMachine.LocationTo:
                    if (true == _model.IsLocationToValid) { goto case StateMachine.Validation; }
                    
                    _state = StateMachine.LocationToResponse;
                    
                    await context.PostAsync($"Куда летите ?");
                    context.Wait(this.ResponseTranslateWhenIntent);
                    break;

                case StateMachine.LocationToResponse:
                    {
                        //var n = await LuisHelpers.ProcessLocation(result.ScoreEntitiesGeography());
                        //if (true == n.isProcessed) { _model.LocationTo = n.geo?.normalized; }
                        await ProcessGeoToPredictive(new EntityRecommendation[] { new EntityRecommendation("DestinationPlaceName", entity: result.Query) }, _model);

                        if (!_model.IsLocationToValid) { await CantUnderstand(context); }
                    }

                    goto case StateMachine.LocationTo;

                case StateMachine.Validation:
                    _state = StateMachine.ValidationResponse;                    
                    {
                        var m = context.MakeMessage();
                        m.Attachments.Add(
                            new ReceiptCard(title:$"Проверьте, пожалуйста, правильно ли я всё понял:",
                                facts: FactsFromModel(_model).ToArray(),
                                buttons: new []
                                {
                                    new CardAction(ActionTypes.ImBack, title: ValidationOk, value: ValidationOk),
                                    new CardAction(ActionTypes.ImBack, title: ValidationError, value: ValidationError),
                                },
                                total:"---" // HACK: stupid Windows Desktop Skype don't show Receipt Card without total field
                                ).ToAttachment());
                        await context.PostAsync(m);                        
                    }                    
                    context.Wait(this.ResponseMessageReceived);
                    break;

                case StateMachine.ValidationResponse:
                    {
                        var m = context.Activity?.AsMessageActivity()?.Text;
                        if (true == ValidationOk.Equals(m, StringComparison.InvariantCultureIgnoreCase)) { _model.IsValid = true; }
                        else if (true == ValidationError.Equals(m, StringComparison.InvariantCultureIgnoreCase))
                        {
                            await context.PostAsync($"Хорошо. Давайте попробуем ещё раз.");
                            _model = new M.Model();
                            goto case StateMachine.Weight;
                        }
                        else
                        {
                            await CantUnderstand(context);
                            goto case StateMachine.Validation;
                        }
                    }

                    goto case StateMachine.Completed;

                case StateMachine.Completed:
                    await context.PostAsync($"Стоимость провоза составит {_rnd.Next(3, 9) * 1000} рублей");
                    context.Done(null as Object);
                    return;
                    
                default:
                    throw new IllegalStateException("NextAction");
            }
            
            if (true == _isDebug) { await LogModel(context); }            
        }

        async private Task CantUnderstand(IDialogContext context)
        {
            await context.PostAsync($"Извините, но я не могу понять Ваш ответ. Попробуйте ещё раз.");
        }

        private static readonly Action<M.Model, M.Dimension>[] EntitiesToDimensions =
        {
            (m, dim) => m.DimLength = dim,
            (m, dim) => m.DimWidth = dim,
            (m, dim) => m.DimHeight = dim,
            (m, dim) => throw new FormatException("Too many dimensions"),
        };

        async public Task IntentAnimalTransportation(IDialogContext context, LuisResult result)
        {
            var entitiesScored = result.ScoreEntities();
            //var centitiesScored = (result?.CompositeEntities?.Scored() ?? new EntityRecommendation[0]).ToArray();
            var entitiesGeographyScored = result.ScoreEntitiesGeography();

            //
            var eAnimal = entitiesScored.ByType("Animal").FirstOrDefault();
            _model.Animal = eAnimal?.Entity;

            //
            ProcessWeight(entitiesScored, _model);

            //
            ProcessAnimalLocationType(entitiesScored, _model);

            //
            var nGeography = (await Task.WhenAll(entitiesGeographyScored.Take(2).Select(er => er.NormalizeGeography()))).Where(n => null != n.normalized).ToArray();

            var nFrom = entitiesScored.GetGeographyNextToEntity("From", nGeography);
            _model.LocationFrom = nFrom?.normalized;

            if (!_model.IsLocationFromValid)
            {
                await ProcessGeoFromPredictive(result.Entities.ToArray(), _model);
            }

            var nTo = entitiesScored.GetGeographyNextToEntity("To", nGeography);
            _model.LocationTo = nTo?.normalized;

            if (!_model.IsLocationToValid)
            {
                await ProcessGeoToPredictive(result.Entities.ToArray(), _model);
            }

            _model.LocationsUnknown = nGeography.Where(n => (n.entity != nFrom?.entity) && (n.entity != nTo?.entity)).Select(n => n.normalized).ToArray();

            await context.PostAsync($"Я вижу, что Вы интересуетесь тарифами и правилами перевозки животных");
        }

        //async public Task PromptResponseWeight(IDialogContext context, IAwaitable<long> weightResponse)
        //{
        //    _model.Weight = new M.Weight(new M.WeightValue(Convert.ToDecimal(await weightResponse, CultureInfo.InvariantCulture)), "кг");
        //    await NextAction(context, null, null);
        //}

        async public Task IntentWeight(IDialogContext context, LuisResult result)
        {
            ProcessWeight(result.ScoreEntities(0.5), _model);
        }

        async public Task IntentDimension(IDialogContext context, LuisResult result)
        {
            try
            {
                var dimensionsCount = result.ScoreEntities().ByType("builtin.number")
                    .Select(er => new M.Dimension(Convert.ToDecimal(er.Entity)))
                    .Zip(EntitiesToDimensions, (dim, a) => { a(_model, dim); return true; }).LongCount();
            }
            catch (FormatException fex) {
                Debug.WriteLine(fex);
            }
            catch(OverflowException oex) {
                Debug.WriteLine(oex);
            }
        }

        public async Task ProcessGeoFromPredictive(EntityRecommendation[] entities, M.Model model)
        {
            if (null == model) { throw new ArgumentNullException(nameof(model)); }

            var locationNormalizedGeo = await entities.ByType("PlaceName").FirstOrDefault().NormalizeGeographyPredictive();
            if (locationNormalizedGeo.normalized != null)
            {
                _model.LocationFrom = locationNormalizedGeo.normalized;
            }
        }

        public async Task ProcessGeoToPredictive(EntityRecommendation[] entities, M.Model model)
        {
            if (null == model) { throw new ArgumentNullException(nameof(model)); }

            var destinationNormalizedGeo = await entities.ByType("DestinationPlaceName").FirstOrDefault().NormalizeGeographyPredictive();
            if (destinationNormalizedGeo.normalized != null)
            {
                _model.LocationTo = destinationNormalizedGeo.normalized;
            }
        }

        private static void ProcessAnimalLocationType(EntityRecommendation[] entities, M.Model model)
        {
            if (null == model) { throw new ArgumentNullException(nameof(model)); }

            var eLuggageLocation = entities.ByType("AnimalLocationType").FirstOrDefault();
            if (null != eLuggageLocation)
            {
                if (eLuggageLocation.Entity.Equals(TravelTypeCabinEn, StringComparison.OrdinalIgnoreCase))
                {
                    model.TravelType = M.TravelType.Cabin;
                }
                else if (eLuggageLocation.Entity.Equals(TravelTypeCheckedInEn, StringComparison.OrdinalIgnoreCase))
                {
                    model.TravelType = M.TravelType.CheckedIn;
                }
            }
        }

        private static void ProcessWeight(EntityRecommendation[] entities, M.Model model)
        {
            if (null == model) { throw new ArgumentNullException(nameof(model)); }

            var eWeight = entities.ByType("Weight").FirstOrDefault();
            if (null != eWeight)
            {
                var eValue = entities.ChildrenByType(eWeight, "builtin.number").FirstOrDefault();
                var eUnit = entities.ChildrenByType(eWeight, "WeightUnit").FirstOrDefault();

                if ((null != eValue) && (null != eUnit))
                {
                    model.Weight = new M.Weight(new M.WeightValue(Convert.ToDecimal(eValue.Entity, CultureInfo.InvariantCulture)), eUnit.Entity);
                }
            }
            else
            {
                var eValue = entities.ByType("builtin.number").FirstOrDefault();
                var eUnit = entities.ByType("WeightUnit").FirstOrDefault();

                if ((null != eValue) /*&& (null != eUnit) && (eUnit.StartIndex == (eValue.EndIndex + 2))*/)
                {
                    model.Weight = new M.Weight(new M.WeightValue(Convert.ToDecimal(eValue.Entity, CultureInfo.InvariantCulture)), eUnit?.Entity??"кг");
                }
            }
        }

        [Serializable]
        public enum StateMachine
        {
            Initial,
            InitialResponse,

            Animal,
            Weight,
            WeightResponse,
            Dimension,
            DimensionResponse,
            TravelType,
            TravelTypeResponse,
            LocationFrom,
            LocationFromResponse,
            LocationTo,
            LocationToResponse,

            Validation,
            ValidationResponse,
            
            Completed,
            
            CantUnderstand,            
        }

        async private Task LogModel(IDialogContext context)
        {
            if (null == context) { throw new ArgumentNullException(nameof(context)); }

            var m = context.MakeMessage();
            // HACK: stupid Windows Desktop Skype don't show Receipt Card without total field
            m.Attachments.Add(new ReceiptCard(title: "(Debug)", facts: FactsFromModelDebug(_model).ToArray(), total:"---").ToAttachment());
            await context.PostAsync(m);                        
        }

        private const String TravelTypeCabin = "В салоне самолёта";
        private const String TravelTypeCheckedIn = "В багажном отсеке";
        private const String TravelTypeCabinEn = "cabin";
        private const String TravelTypeCheckedInEn = "baggage compartment";
        private const String ValidationOk = "Всё правильно";
        private const String ValidationError = "Есть ошибка";

        public static class M
        {
            public const String None = "<b>none</b>";
            public const String CrLf = "<br/>";
            public const String NoData = "нет данных";

            private static String ToStringWrap(String prefix, String source)
            {
                return $"{prefix}" + ((null != source) ? $"{M.CrLf}==={M.CrLf}{source}{M.CrLf}===" : M.None);
            }

            [Serializable]
            public class DecimalPositiveNotZero : IEquatable<DecimalPositiveNotZero>
            {
                private decimal Value { get; }

                public DecimalPositiveNotZero(Decimal value)
                {
                    if (value <= 0) { throw new ArgumentOutOfRangeException(nameof(value)); }

                    Value = value;
                }

                public override String ToString()
                {
                    return $"{Value}";
                }

                #region Equality

                public bool Equals(DecimalPositiveNotZero other)
                {
                    if (ReferenceEquals(null, other)) { return false; }
                    if (ReferenceEquals(this, other)) { return true; }
                    return Value == other.Value;
                }

                public override bool Equals(object obj)
                {
                    if (ReferenceEquals(null, obj)) { return false; }
                    if (ReferenceEquals(this, obj)) { return true; }
                    if (obj.GetType() != this.GetType()) { return false; }
                    return Equals((DecimalPositiveNotZero) obj);
                }

                public override int GetHashCode()
                {
                    return Value.GetHashCode();
                }

                public static bool operator ==(DecimalPositiveNotZero left, DecimalPositiveNotZero right)
                {
                    return Equals(left, right);
                }

                public static bool operator !=(DecimalPositiveNotZero left, DecimalPositiveNotZero right)
                {
                    return !Equals(left, right);
                }

                #endregion
            }

            [Serializable]
            public sealed class WeightValue : DecimalPositiveNotZero
            {
                public WeightValue(Decimal value) : base(value) {}
            }

            [Serializable]
            public sealed class Dimension : DecimalPositiveNotZero
            {
                public Dimension(Decimal value) : base(value) {}
            }

            [Serializable]
            public enum TravelType
            {
                Unknown,
                Cabin,
                CheckedIn
            }

            [Serializable]
            public class Weight
            {
                public WeightValue Value { get; private set; }
                public String Unit { get; private set; }

                public Weight(WeightValue value, String unit)
                {
                    Value = value;
                    Unit = unit;
                }

                public override String ToString()
                {
                    return $"Value: {Value?.ToString() ?? M.None}\nUnit: {Unit ?? M.None}";
                }
            }

            [Serializable]
            public class Model
            {
                public String Animal { get; set; }

                public Weight Weight { get; set; }
                public bool IsWeightValid => null != Weight; 

                public Dimension DimLength { get; set; }
                public Dimension DimWidth { get; set; }
                public Dimension DimHeight { get; set; }

                public bool IsDimensionValid => (null != DimLength) && (null != DimWidth) && (null != DimHeight);
                
                public TravelType TravelType { get; set; }
                public bool IsTravelTypeValid => (TravelType.Cabin == TravelType) || (TravelType.CheckedIn == TravelType); 

                public String LocationFrom { get; set; }
                public bool IsLocationFromValid => null != LocationFrom;
                public String LocationTo { get; set; }
                public bool IsLocationToValid => null != LocationTo;

                public String[] LocationsUnknown { get; set; }

                public bool IsValid { get; set; }
            
                public override String ToString()
                {
                    var unknown = LocationsUnknown?.AggregateWithDelimiter(new StringBuilder(), acc => acc.Append(M.CrLf), (acc, e) => acc.Append($"Location: {e ?? M.None}")).ToString();
                    unknown = (false == String.IsNullOrEmpty(unknown)) ? unknown : null;

                    return $"Animal: {Animal ?? M.None}{M.CrLf}" + ToStringWrap("Weight: ", Weight?.ToString()) + M.CrLf + ToStringWrap("Dimension: ",
                               $"Length: {DimLength?.ToString() ?? M.None}{M.CrLf}Width: {DimWidth?.ToString() ?? M.None}{M.CrLf}Height: {DimHeight?.ToString() ?? M.None}") + M.CrLf + 
                           $"Travel type: {TravelType}{M.CrLf}" + 
                           $"From: {LocationFrom ?? M.None}{M.CrLf}To: {LocationTo ?? M.None}{M.CrLf}" + ToStringWrap("Unknown locations: ", unknown) + M.CrLf +
                           $"Validation: {IsValid}";
                }
            }
        }

        private static IEnumerable<Fact> FactsFromModelDebug(M.Model m)
        {
            if (null == m)
            {
                new Fact("Model", "null");
                yield break;
            }
            
            yield return new Fact("Open data");
            yield return new Fact("-----------------------");
            foreach (var fact in FactsFromModel(m)) { yield return fact; }
            yield return new Fact("-----------------------");
            
            yield return new Fact("Internal data");
            yield return new Fact("-----------------------");
            yield return new Fact("IsValid", $"{m.IsValid}");
            
            yield return new Fact("Unknown locations");
            if (null != m?.LocationsUnknown) { foreach (var l in m.LocationsUnknown) { yield return new Fact(l); } }
        }
        
        private static IEnumerable<Fact> FactsFromModel(M.Model m)
        {
            yield return new Fact("Животное", m?.Animal ?? M.NoData);
            yield return new Fact("Вес", (null != m?.Weight) ? $"{m.Weight.Value} {m.Weight.Unit}" : M.NoData);
            
            yield return new Fact("Размеры клетки", "");
            yield return new Fact("Длина", FactValueFromDimension(m?.DimLength));
            yield return new Fact("Ширина", FactValueFromDimension(m?.DimWidth));
            yield return new Fact("Высота", FactValueFromDimension(m?.DimHeight));

            switch (m?.TravelType)
            {
                case M.TravelType.Cabin:
                    yield return new Fact("Перевозка в салоне самолёта", "");                    
                    break;
                case M.TravelType.CheckedIn:
                    yield return new Fact("Перевозка в багажном отсеке", "");
                    break;
            }

            yield return new Fact("Маршрут", "");            
            yield return new Fact("Вылет", m?.LocationFrom ?? M.NoData);
            yield return new Fact("Прилёт", m?.LocationTo ?? M.NoData);
        }

        private static String FactValueFromDimension(M.Dimension d) => (null != d) ? $"{d} см" : M.NoData;
    }
}
