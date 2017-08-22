using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using Microsoft.Bot.Connector;

namespace testSibiraBot2.Dialogs 
{
    [Serializable]
    public abstract class SibiraLuisDialog<TResult> : LuisDialog<TResult> 
    {
        protected SibiraLuisDialog(Boolean isDebug = false, params ILuisService[] services) : base(services)
        {
            _isDebug = isDebug;
        }

        protected bool _isDebug { get; set; } = false;
        
        async protected virtual Task<bool> CheckDebug(IDialogContext context, IMessageActivity m)
        {
            if (false == ":debug:".Equals(m.Text, StringComparison.InvariantCulture)) { return false; }
            
            _isDebug = !_isDebug;
            await context.PostAsync($"(Debug is {(_isDebug ? "on" : "off")})");
            
            return true;
        }

        async protected Task Translate(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var message = await result;
            message.Text = await message.Text.Translate();
            
            if (true == _isDebug) { await context.PostAsync($"(Translate){RootLuisDialog.M.CrLf}{message.Text }"); }
        }

        protected abstract Task NextAction(IDialogContext context, IAwaitable<IMessageActivity> message, LuisResult result);
        
        public static class I
        {
            public const String None = "None";
            public const String BaggageRestrictions = "BaggageRestrictions";
            public const String AnimalTransportation = "AnimalTransportation";
            public const String Weight = "Weight";
            public const String Dimension = "Dimension";
        }

        [LuisIntent("")]
        [LuisIntent(I.None)]
        [LuisIntent(I.BaggageRestrictions)]
        [LuisIntent(I.AnimalTransportation)]
        [LuisIntent(I.Weight)]        
        [LuisIntent(I.Dimension)]
        async public Task Intent(IDialogContext context, IAwaitable<IMessageActivity> message, LuisResult result)
        {
            await NextAction(context, message, result);
        }

        async protected Task ResponseMessageReceived(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            if (true == await CheckDebug(context, await result)) { return; }

            try
            {
                await MessageReceived(context, result);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        async protected Task ResponseTranslateWhenIntent(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            if (true == await CheckDebug(context, await result)) { return; }
            
            await Translate(context, result);
            try
            {
                await MessageReceived(context, result);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }
    }
}