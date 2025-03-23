using System;
using System.Collections.Generic;
using OoLunar.DocBot.Interactivity.Moments.Choose;
using OoLunar.DocBot.Interactivity.Moments.Confirm;
using OoLunar.DocBot.Interactivity.Moments.Pagination;
using OoLunar.DocBot.Interactivity.Moments.Pick;
using OoLunar.DocBot.Interactivity.Moments.Prompt;

namespace OoLunar.DocBot.Interactivity
{
    public sealed record ProcrastinatorConfiguration
    {
        public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromSeconds(30);
        public Dictionary<Type, IComponentCreator> ComponentCreators { get; init; } = new()
        {
            { typeof(IChooseComponentCreator), new ChooseDefaultComponentCreator() },
            { typeof(IConfirmComponentCreator), new ConfirmDefaultComponentCreator() },
            { typeof(IPaginationComponentCreator), new PaginationDefaultComponentCreator() },
            { typeof(IPickComponentCreator), new PickDefaultComponentCreator() },
            { typeof(IPromptComponentCreator), new PromptDefaultComponentCreator() }
        };

        public TComponentCreator GetComponentCreatorOrDefault<TComponentCreator, TDefaultComponentCreator>()
            where TComponentCreator : IComponentCreator
            where TDefaultComponentCreator : TComponentCreator, new()
                => ComponentCreators.TryGetValue(typeof(TComponentCreator), out IComponentCreator? creator)
                    ? (TComponentCreator)creator
                    : new TDefaultComponentCreator();
    }
}
