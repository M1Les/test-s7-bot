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
using testSibiraBot2.Services;

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
    [LuisModel("13ba4fe3-37d5-4cf9-a106-e746a641b2c5", "268cb802b893461591e26a07c2dac50f", staging: true)]
    [Serializable]
    public class RootLuisDialog : SibiraLuisDialog<Object>
    {
        private StateMachine _state { get; set; } = StateMachine.Initial;
        private IBookingService _booking { get; set; } = new MockBookingService();
        
        async public override Task StartAsync(IDialogContext context)
        {
            //Microsoft.Bot.Builder.Dialogs.Extensions
            //    .Wait((IDialogStack) context, new ResumeAfter<IMessageActivity>(this.MessageReceived));

            await context.PostAsync($"Здравствуйте!");
            
            await  NextAction(context, null, null);
        }

        async protected override Task NextAction(IDialogContext context, IAwaitable<IMessageActivity> message, LuisResult result)
        {
            switch (_state)
            {
                case StateMachine.Initial:
                    _state = StateMachine.InitialResponse;
                    _booking = new MockBookingService();
                    
                    context.Wait(ResponseTranslateWhenIntent);
                    break;
                
                case StateMachine.InitialResponse:
                    switch (result.TopScoringIntent.Intent)
                    {
                        case I.AnimalTransportation:
                            _state = StateMachine.AnimalTransportationCompleted;

                            context.Call(new AnimalsLuisDialog(result, _isDebug), ChildDialogCompleted);
                            return;

                        case I.BaggageRestrictions:
                            _state = StateMachine.BaggageRestrictionsCompleted;

                            context.Call(new BaggageLuisDialog(_booking, result, _isDebug), ChildDialogCompleted);
                            return;
                    }
                    
                    await context.PostAsync($"Извините, но я не могу понять Ваш вопрос.");
                    goto case StateMachine.Initial;

                case StateMachine.AnimalTransportationCompleted:
                case StateMachine.BaggageRestrictionsCompleted:
                    
                case StateMachine.Completed:
                    await context.PostAsync($"Если у Вас есть ещё вопросы, пожалуйста спрашивайте.");
                    goto case StateMachine.Initial;
                    
                default:
                    throw new IllegalStateException("NextAction");
            }
        }

        async private Task ChildDialogCompleted(IDialogContext context, IAwaitable<Object> result)
        {
            await NextAction(context, null, null);
        }

        async private Task CantUnderstand(IDialogContext context)
        {
            await context.PostAsync($"Извините, но я не могу понять Ваш ответ. Попробуйте ещё раз.");
        }
        
        async private Task ResponseMessageReceived(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            if (true == await CheckDebug(context, await result)) { return; }
            
            await MessageReceived(context, result);
        }
        
        async private Task ResponseTranslateWhenIntent(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            if (true == await CheckDebug(context, await result)) { return; }
            
            await Translate(context, result);
            await MessageReceived(context, result);
        }

        async protected override Task<Boolean> CheckDebug(IDialogContext context, IMessageActivity m)
        {
            var chain = await base.CheckDebug(context, m);
            
            if (true == chain) { return chain; }
            if (false == ":stub:".Equals(m.Text, StringComparison.InvariantCulture)) { return false; }
            
            await context.PostAsync($"(Debug)<br>{_booking.ToString()}");
            return true;
        }

        public static class M
        {
            public const String None = "<b>none</b>";
            public const String CrLf = "<br/>";
            public const String NoData = "нет данных";
        }
        
        [Serializable]
        public enum StateMachine
        {
            Initial,
            InitialResponse,

            BaggageRestrictionsCompleted,
            AnimalTransportationCompleted,
            
            Completed,
            
            CantUnderstand,            
        }
    }
}
