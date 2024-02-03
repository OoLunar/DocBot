using System;
using System.Threading.Tasks;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Trees;

namespace OoLunar.DocBot.ContextChecks
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Delegate)]
    public sealed class RequireOwnerOrSelfBot : RequireOwnerAttribute
    {
        public override async Task<bool> ExecuteCheckAsync(CommandContext context) => await base.ExecuteCheckAsync(context) || context.Client.CurrentUser.Id == context.User.Id;
    }
}
