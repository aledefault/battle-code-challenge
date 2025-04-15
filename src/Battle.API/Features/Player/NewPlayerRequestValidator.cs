using Battle.API.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Battle.API.Features.Player;

public class NewPlayerRequestValidator : AbstractValidator<NewPlayerRequest>
{
    public NewPlayerRequestValidator(IOptions<BattleStatsOptions> battleStatsOptions)
    {
        var options = battleStatsOptions.Value;
        RuleFor(x => x.Username)
            .NotEmpty().MinimumLength(3).MaximumLength(25)
            .WithMessage("Username must be between 3 and 25 characters");
        
        RuleFor(x => x.Email).EmailAddress().WithMessage("Not a valid email address.");
        
        RuleFor(x => x.Attack)
            .InclusiveBetween(options.MinAttack, options.MaxAttack)
            .WithMessage($"Attack must be between {options.MinAttack} and {options.MaxAttack}.");

        RuleFor(x => x.Defense)
            .InclusiveBetween(options.MinDefense, options.MaxDefense)
            .WithMessage($"Defense must be between {options.MinDefense} and {options.MaxDefense}.");

        RuleFor(x => x)
            .Must(x => (x.Attack + x.Defense) <= options.MaxPointsToDistribute)
            .WithMessage($"The sum of Attack and Defense must equal {options.MaxPointsToDistribute}.");
    }
}