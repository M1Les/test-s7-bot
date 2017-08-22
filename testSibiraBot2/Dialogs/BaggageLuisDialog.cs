using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Chronic.Tags.Repeaters;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using Microsoft.Bot.Connector;
using testSibiraBot2.Services;

namespace testSibiraBot2.Dialogs
{
    [LuisModel("13ba4fe3-37d5-4cf9-a106-e746a641b2c5", "268cb802b893461591e26a07c2dac50f", staging: true)]
    [Serializable]
    public class BaggageLuisDialog : SibiraLuisDialog<Object>
    {
        private IBookingService _booking { get; }

        [NonSerialized]
        private readonly LuisResult _start;
        
        public BaggageLuisDialog(IBookingService booking, LuisResult start, Boolean isDebug = false, params ILuisService[] services) : base(isDebug, services)
        {
            if (null == booking) { booking = new MockBookingService(); }
            
            _start = start;
            _booking = booking;
        }

        async public override Task StartAsync(IDialogContext context)
        {
            //Microsoft.Bot.Builder.Dialogs.Extensions
            //    .Wait((IDialogStack) context, new ResumeAfter<IMessageActivity>(this.MessageReceived));

            await NextAction(context, null, _start);
        }
        
        private StateMachine _state { get; set; } = StateMachine.Initial;
        
        async protected override Task NextAction(IDialogContext context, IAwaitable<IMessageActivity> message, LuisResult result)
        {
            switch (_state)
            {
                case StateMachine.Initial:
                     _state = StateMachine.InitialResponse;
                
                    //_model= new M.Model();
                
                    goto case StateMachine.InitialResponse;
            
                case StateMachine.InitialResponse:
                    if (I.BaggageRestrictions != result.TopScoringIntent.Intent)
                    {
                        await context.PostAsync($"Извините, но я не могу понять Ваш вопрос.");
                        
                        goto case StateMachine.Completed;
                    }

                    await IntentBaggageRestrictions(context, result);
                    goto case StateMachine.AuthenticationCheck;

                case StateMachine.AuthenticationCheck:
                    if (false == _booking.IsLoggedIn())
                    {
                        _state = StateMachine.ReservationByCodeResponse;
                        
                        await context.PostAsync("Я вижу, что Вы не вошли в нашу систему. Пожалуйста, введите код Вашей брони:");
                        context.Wait(ResponseMessageReceived);
                        break;
                    }

                    await context.PostAsync("Я вижу, что Вы уже выполнили вход в нашу систему. Сейчас я поищу Вашу бронь...");
                    goto case StateMachine.ReservationByAuthentication;
                    
                case StateMachine.ReservationByAuthentication:    
                {
                    var reservations = _booking.GetReservations() ?? new Reservation[0];
                    if (reservations.Count <= 0)
                    {
                        await context.PostAsync("У Вас нет забронированных билетов. Извините, но в этом случае я пока не могу Вам помочь.");
                        goto case StateMachine.Completed;
                    }

                    if (reservations.Count > 1)
                    {
                        await context.PostAsync("У Вас больше одной брони. Извините, но с такой ситуацией я пока справляться не умею.");
                        goto case StateMachine.Completed;
                    }

                    _Reservation = reservations[0];

                    goto case StateMachine.ProcessReservation;
                }
                    
                case StateMachine.ReservationByCodeResponse:
                {
                    var m = await message;
                    var msgText = m.Text;
                    
                    var r = _booking.GetReservations()
                        .FirstOrDefault(res => (null != res?.Id) && res.Id.Equals(msgText, StringComparison.InvariantCultureIgnoreCase));
                    if (null == r)
                    {
                        _state = StateMachine.ReservationByCodeFailedResponse;
                        
                        await context.PostAsync($"Я не могу найти Вашу бронь с кодом '{msgText}'. Хотите попробовать ввести другой код ?");
                        context.Wait(ResponseMessageReceived);
                        break;
                    }
                    
                    _Reservation = r;
                    goto case StateMachine.ProcessReservation;                        
                }

                case StateMachine.ReservationByCodeFailedResponse:
                {
                    var m = await message;
                    var msgText = m.Text;

                    if ("ok".Equals(msgText, StringComparison.InvariantCultureIgnoreCase) ||
                        "yes".Equals(msgText, StringComparison.InvariantCultureIgnoreCase) ||
                        "ок".Equals(msgText, StringComparison.InvariantCultureIgnoreCase) ||
                        "да".Equals(msgText, StringComparison.InvariantCultureIgnoreCase))
                    {
                        _state = StateMachine.ReservationByCodeResponse;
                        
                        await context.PostAsync("Пожалуйста, введите код Вашей брони:");
                        context.Wait(ResponseMessageReceived);
                        break;
                    }

                    goto case StateMachine.Completed;
                }

                case StateMachine.ProcessReservation:
                    await ProceedWithReservation(context);
                    goto case StateMachine.Completed;
                    
                case StateMachine.Completed:
                    context.Done(null as Object);
                    return;
                    
                default:
                    throw new IllegalStateException("NextAction");
            }
            
            //if (true == _isDebug) { await LogModel(context); }            
        }

        private Reservation _Reservation { get; set; }

        async public Task IntentBaggageRestrictions(IDialogContext context, LuisResult result)
        {
            var score = result.TopScoringIntent.Score;

            if (true == _isDebug)
            {
                await context.PostAsync($"(Debug) query='{result.Query}'");
                await context.PostAsync($"(Debug) intent_score={score}");
            }
            
//            if (score < 0.95)
//            {
//                await None(context, result);
//                return;
//            }

            await context.PostAsync($"Пожалуйста, подождите немного пока я изучаю Ваш вопрос...");

            var entitiesGeographyScored = result.ScoreEntitiesGeography();
            var nGeography = (await Task.WhenAll(entitiesGeographyScored.Select(er => er.NormalizeGeography(cityFullName: false))))
                .Where(n => null != n.normalized).ToArray();

            _Cities = nGeography.Where(ng => LuisHelpers.BuiltInGeographyCity.Equals(ng.entity.Type, StringComparison.InvariantCultureIgnoreCase))
                .Select(ng => ng.normalized).ToArray();
            _Countries = nGeography.Where(ng => LuisHelpers.BuiltInGeographyCountry.Equals(ng.entity.Type, StringComparison.InvariantCultureIgnoreCase))
                .Select(ng => ng.normalized).ToArray();

//            var _Cities = result?.Entities
//                ?.Where(e =>
//                    ("builtin.geography.city".Equals(e.Type, StringComparison.InvariantCultureIgnoreCase))
//                    && ((e.Score ?? -1.0) >= 0.5))
//                .ToArray() ?? new EntityRecommendation[0];
//
//            var _Countries = result?.Entities
//                ?.Where(e =>
//                    ("builtin.geography.country".Equals(e.Type, StringComparison.InvariantCultureIgnoreCase))
//                    && ((e.Score ?? -1.0) >= 0.5))
//                .Select(e => e.Entity)
//                .ToArray() ?? new String[0];

//            // normalize countries
//            if (_Countries.Length > 0)
//            {
//                // ((BingMapsRESTToolkit.Location)(r.ResourceSets[0x00000000].Resources[0x00000000])).Address.CountryRegion
//                // ((BingMapsRESTToolkit.Location)(r.ResourceSets[0x00000000].Resources[0x00000000])).Confidence
//                // r.StatusCode
//                // r.errorDetails
//
//                _Countries = (await Task.WhenAll(
//                    _Countries.Select(ct => ServiceManager.GetResponseAsync(new GeocodeRequest()
//                    {
//                        BingMapsKey = "AngIydqdFB0kbCLQDr3vVqPbHiDOLYvCBNreIYwxtCoekUKBpuKwTjUsHcmzg3jk",
//                        Culture = "ru-ru",
//                        Query = ct
//                    })))
//                )
//                .Where(resp => 200 == resp.StatusCode)
//
//                .SelectMany(resp => resp.ResourceSets)
//                .SelectMany(rs => rs.Resources)
//                
//                .OfType<Location>()
//                .Where(res => "High".Equals(res.Confidence, StringComparison.InvariantCultureIgnoreCase))
//                //.Where(res => "CountryRegion".Equals(res.EntityType, StringComparison.InvariantCultureIgnoreCase))
//                .Select(res => res.Address?.CountryRegion)
//                .Where(cr => false == String.IsNullOrEmpty(cr))
//                .ToArray();
//
//                var iii = 0;
//                iii++;
//
//                //foreach (var ct in cities) {
//                //    var r = await ServiceManager.GetResponseAsync(new GeocodeRequest()
//                //    {
//                //        BingMapsKey = "AngIydqdFB0kbCLQDr3vVqPbHiDOLYvCBNreIYwxtCoekUKBpuKwTjUsHcmzg3jk",
//                //        Query = ct.Entity
//                //    });
//
//                //    var countriesFromCities = r.ResourceSets.SelectMany(rs => rs.Resources)
//                //        .OfType<Location>()
//                //        .Where(res => "High".Equals(res.Confidence, StringComparison.InvariantCultureIgnoreCase))
//                //        .Select(res => res.Address?.CountryRegion)
//                //        .ToArray();
//
//                //}
//
//            }
        }

        private String[] _Countries { get; set; }
        private String[] _Cities { get; set; }

        async private Task ProceedWithReservation(IDialogContext context)
        {
            var segments = _Reservation?.Segments;
            if ((null == segments) || (segments.Count <= 0))
            {
                await context.PostAsync("У меня проблемы с доступом к системе бронирования билетов авиакомпании. Пожалуйста, попробуйте задать Ваш вопрос ещё раз через несколько минут.");
                return;
            }

            var isGeoDataPresent = (_Cities.Length > 0) || (_Countries.Length > 0);
            if (true == isGeoDataPresent)
            {
                segments = Enumerable.Concat(
                    Enumerable.Concat(
                        segments.Where(s => _Cities.Contains(s.To.City, StringComparer.InvariantCultureIgnoreCase)),
                        segments.Where(s => _Cities.Contains(s.From.City, StringComparer.InvariantCultureIgnoreCase))),
                    Enumerable.Concat(
                        segments.Where(s => _Countries.Contains(s.To.Country, StringComparer.InvariantCultureIgnoreCase)),
                        segments.Where(s => _Countries.Contains(s.From.Country, StringComparer.InvariantCultureIgnoreCase)))
                ).Distinct(EqualityComparer<Segment>.Default)
                .ToArray();

                if (segments.Count > 1)
                {
                    await context.PostAsync(@"Я нашла в Вашей брони несколько сегментов подходящих к указанным Вами городам\странам.");
                }

                if (1 == segments.Count)
                {
                    await context.PostAsync(@"Вот подходящая к указанным Вами городам\странам бронь.");
                }

                if (0 == segments.Count)
                {
                    await context.PostAsync(@"У Вас нет брони подходящей к указанным Вами городам\странам.");
                }
            }
            else 
            {
                await context.PostAsync($"Я нашла Вашу бронь с кодом {_Reservation.Id}.");

                if (segments.Count > 1) { await context.PostAsync("В Вашей брони больше одного сегмента."); }
            }

            if (segments.Count > 0)
            {
                var resultMessage = context.MakeMessage();
                resultMessage.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                resultMessage.Attachments = new List<Attachment>();

                var idx = 0;
                foreach (var segment in segments)
                {
                    var heroCard = new HeroCard()
                    {
                        Subtitle = 
                            $"Из {segment.From.Code} ({segment.From.CityRu}, {segment.From.CountryRu})\n в {segment.To.Code} ({segment.To.CityRu}, {segment.To.CountryRu})",
                        Title = $"Рейс {segment.Flight.Code}",
                        Text = $"Багаж: {segment.TariffBaggage}",
                        //Buttons = new[]
                        //{
                        //    new CardAction()
                        //    {
                        //        Title = "Выбрать этот сегмент",
                        //        Type = ActionTypes.PostBack,
                        //        Value = idx
                        //    }
                        //}
                    };

                    resultMessage.Attachments.Add(heroCard.ToAttachment());
                    idx++;
                }

                await context.PostAsync(resultMessage);
            }
        }
        
        [Serializable]
        public enum StateMachine
        {
            Initial,
            InitialResponse,

            AuthenticationCheck,
            
            ReservationByAuthentication,
            ReservationByCodeResponse,
            ReservationByCodeFailedResponse,
            
            ProcessReservation,
            
            Completed,
            
            CantUnderstand,            
        }
    }
}