using Battle.API.Features.Player;
using Battle.API.Options;
using FluentAssertions;

namespace Battle.API.UnitTests;

public class NewPlayerRequestValidatorUnitTests
{
    [Theory]
    [InlineData(80, 70, 1, 1, 100, 100, true)]
    [InlineData(1, 100, 1, 1, 100, 100, true)]
    [InlineData(100, 1, 1, 1, 100, 100, true)]
    [InlineData(100, 50, 1, 1, 100, 100, true)]
    [InlineData(50, 100, 1, 1, 100, 100, true)]
    [InlineData(80, 71, 1, 1, 100, 100, false)]
    [InlineData(1, 101, 1, 1, 100, 100, false)]
    [InlineData(101, 1, 1, 1, 100, 100, false)]
    [InlineData(100, 51, 1, 1, 100, 100, false)]
    [InlineData(51, 100, 1, 1, 100, 100, false)]
    public void Should_validate_inconsistent_attack_defense_distribution(
        int attack,
        int defense,
        int minAttack,
        int minDefense,
        int maxAttack,
        int maxDefense,
        bool expectedResult)
    {
        var defaultOptions = Microsoft.Extensions.Options.Options.Create(
            new BattleStatsOptions
            {
                MinAttack = minAttack,
                MinDefense = minDefense,
                MaxAttack = maxAttack,
                MaxDefense = maxDefense
            });
        var newPlayerRequest = new NewPlayerRequest { Username = "Glados", Attack = attack, Defense = defense, Email = "a@a"};
        var validator = new NewPlayerRequestValidator(defaultOptions);

        var result = validator.Validate(newPlayerRequest);

        result.IsValid.Should().Be(expectedResult);
    }
    
    [Theory]
    [InlineData("", false)]
    [InlineData("not a email", false)]
    [InlineData("a@a.com", true)]
    [InlineData("a@al", true)]
    public void Should_validate_email(string email, bool expectedResult)
    {
        var defaultOptions = Microsoft.Extensions.Options.Options.Create(new BattleStatsOptions());
        var newPlayerRequest = new NewPlayerRequest { Email = email, Username = "Glados", Attack = 100, Defense = 50 };
        var validator = new NewPlayerRequestValidator(defaultOptions);

        var result = validator.Validate(newPlayerRequest);

        result.IsValid.Should().Be(expectedResult);
    }
    
    [Theory]
    [InlineData("", false)]
    [InlineData("a", false)]
    [InlineData("aa", false)]
    [InlineData("thedeborerofmenandmatter1", true)] // 25
    [InlineData("thedeborerofmenandmatterXX", false)] // 26
    [InlineData("aaa", true)]
    public void Should_validate_username(string username, bool expectedResult)
    {
        var defaultOptions = Microsoft.Extensions.Options.Options.Create(new BattleStatsOptions());
        var newPlayerRequest = new NewPlayerRequest { Username = username, Attack = 100, Defense = 50, Email = "a@a"};
        var validator = new NewPlayerRequestValidator(defaultOptions);

        var result = validator.Validate(newPlayerRequest);

        result.IsValid.Should().Be(expectedResult);
    }
}