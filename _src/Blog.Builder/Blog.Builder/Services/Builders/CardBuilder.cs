﻿using Blog.Builder.Exceptions;
using Blog.Builder.Models;
using Blog.Builder.Models.Templates;
using Blog.Builder.Services.Interfaces;
using Blog.Builder.Services.Interfaces.Builders;
using Newtonsoft.Json;
using RazorEngine.Templating;
using WebMarkupMin.Core;

namespace Blog.Builder.Services.Builders;

internal record class ArticleCardBuilderResult
{
    public string CardHtml { get; init; } = string.Empty;
    public DateTime DateCreated { get; init; }
}
internal record class OtherCardBuilderResult
{
    public string CardHtml { get; init; } = string.Empty;
    public int Position { get; init; }
    public bool IsSticky { get; init; }
}

internal class CardBuilder : ICardBuilder
{
    private readonly IRazorEngineService _templateEngine;
    private readonly ITemplateProvider _templateProvider;
    private static readonly List<ArticleCardBuilderResult> ArticleCards = new List<ArticleCardBuilderResult>();
    private static readonly List<OtherCardBuilderResult> OtherCards = new List<OtherCardBuilderResult>();

    private readonly object __lockObj = new object();

    public CardBuilder(
            IRazorEngineService templateEngine,
            ITemplateProvider templateProvider,
            IMarkupMinifier markupMinifier
            )
    {
        ArgumentNullException.ThrowIfNull(templateEngine);
        ArgumentNullException.ThrowIfNull(templateProvider);
        ArgumentNullException.ThrowIfNull(markupMinifier);

        _templateEngine = templateEngine;
        _templateProvider = templateProvider;
    }


    private string CreateCardHtml<T>(T cardData) where T : CardModelBase
    {
        ArgumentNullException.ThrowIfNull(cardData);
        cardData.Validate();

        return _templateEngine.RunCompile(
                                        _templateProvider.Get<T>(),
                                        Guid.NewGuid().ToString(),
                                        typeof(T),
                                        cardData);

    }

    public void AddCard(string directory)
    {
        ExceptionHelpers.ThrowIfPathNotExists(directory);

        var jsonFileContent = File.ReadAllText(Path.Combine(directory, "card.json"));
        var cardDataBase = JsonConvert.DeserializeObject<CardModelBase>(jsonFileContent);
        ExceptionHelpers.ThrowIfNull(cardDataBase);
        cardDataBase.Validate();

        //is there a way to load the type directly like: 
        // var cardData = cardDataBase as >>cardDataBase.CardType.Name<<;
        string cardHtml = cardDataBase.TemplateDataModel switch
        {
            nameof(CardSearchModel) => CreateCardHtml(CardSearchModel.FromBase(cardDataBase)),
            nameof(CardImageModel) => CreateCardHtml(CardImageModel.FromBase(cardDataBase)),
            nameof(CardArticleModel) => throw new Exception($"{AddCard} cannot be used with {nameof(CardArticleModel)}, use {AddArticleCard} instead."),
            _ => throw new Exception($"{cardDataBase.TemplateDataModel} switch is missing."),
        };
        ExceptionHelpers.ThrowIfNullOrWhiteSpace(cardHtml);

        lock (__lockObj)
        {
            OtherCards.Add(new OtherCardBuilderResult
            {
                CardHtml = cardHtml,
                Position = cardDataBase.Position,
                IsSticky = cardDataBase.IsSticky
            });
        }
    }

    public void AddArticleCard(CardArticleModel cardData, DateTime dateCreated)
    {
        var cardHtml = CreateCardHtml(cardData);
        ExceptionHelpers.ThrowIfNullOrWhiteSpace(cardHtml);

        lock (__lockObj)
        {
            ArticleCards.Add(new ArticleCardBuilderResult
            {
                CardHtml = cardHtml,
                DateCreated = dateCreated
            });
        }
    }


    public string GetHtml(int pageIndex, int cardsPerPage)
    {
        var cards = ArticleCards.OrderByDescending(x => x.DateCreated)
                                .Select(x => x.CardHtml)
                                .ToList();
        var stickyCardsNum = OtherCards.Count(x => x.IsSticky);

        //add the none-sticky cards to their correct position
        foreach (var card in OtherCards.Where(x => !x.IsSticky).OrderBy(x => x.Position))
        {
            if (card.Position > cards.Count())
            {
                cards.Add(card.CardHtml);
            }
            else
            {
                cards.Insert(card.Position, card.CardHtml);
            }
        }

        //select the cards that will play a role in the paging,
        // sticky cards will appear in every page anyway so they can be excluded
        if (stickyCardsNum >= cardsPerPage)
        {
            throw new Exception($"Number of cards per page ({nameof(AppSettings)}.{nameof(AppSettings.CardsPerPage)}) must be bigger than the number of sticky cards (" +
                $"check property {nameof(CardModelBase.IsSticky)} in all additional cards).");
        }
        cards = cards.Skip(pageIndex * (cardsPerPage - stickyCardsNum)).Take(cardsPerPage - stickyCardsNum).ToList();

        //don't create pages with just sticky cards, it makes no sense
        // this action should have been avoided from GetCardsNumber method
        if (cards.Count == 0)
        {
            throw new Exception($"A request to build a page with just sticky page is not valid. This action should have been avoided by the {nameof(this.GetCardsNumber)} method.");
        }

        //add the sticky cards to their correct position
        foreach (var card in OtherCards.Where(x => x.IsSticky).OrderBy(x => x.Position))
        {
            if (card.Position > cards.Count())
            {
                cards.Add(card.CardHtml);
            }
            else
            {
                cards.Insert(card.Position, card.CardHtml);
            }
        }

        //return the html
        return string.Join(string.Empty, cards.ToArray());
    }

    public int GetCardsNumber(int cardsPerPage)
    {
        var stickyCardsNum = OtherCards.Count(x => x.IsSticky);
        if (stickyCardsNum >= cardsPerPage)
        {
            throw new Exception($"Number of cards per page ({nameof(AppSettings)}.{nameof(AppSettings.CardsPerPage)}) must be bigger than the number of sticky cards (" +
                $"check property {nameof(CardModelBase.IsSticky)} in all additional cards).");
        }

        //calculate the number of pages withouth the sticky cards,
        // they will present in each page anyway
        var totalPages = Math.Ceiling(ArticleCards.Count / (decimal)(cardsPerPage - stickyCardsNum));

        //return the number of article cards
        // plus the number of additional cards that are not sticky
        // plus the sticky cards that will exist in every page
        var totalCount = ArticleCards.Count
                            + OtherCards.Count(x => !x.IsSticky)
                            + OtherCards.Count(x => x.IsSticky) * (int)totalPages;

        //if last page contains just the sticky cards, then do not create a page just for that
        if (totalCount % cardsPerPage <= stickyCardsNum)
        {
            totalCount = totalCount - totalCount % cardsPerPage;
        }

        return totalCount;
    }
}