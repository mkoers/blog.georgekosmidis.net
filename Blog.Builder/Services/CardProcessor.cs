﻿using Blog.Builder.Exceptions;
using Blog.Builder.Interfaces;
using Blog.Builder.Interfaces.Builders;
using Blog.Builder.Interfaces.Crawlers;
using Blog.Builder.Models;
using Blog.Builder.Models.Crawlers;
using Blog.Builder.Models.Templates;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SixLabors.ImageSharp;

namespace Blog.Builder.Services;

/// <inheritdoc/>
internal class CardProcessor : ICardProcessor
{
    private readonly ICardBuilder _cardBuilder;
    private readonly IMeetupEventCrawler _meetupEventCrawler;
    private readonly IFileEventCrawler _fileEventCrawler;
    private readonly AppSettings appSettings;

    public CardProcessor(ICardBuilder cardBuilder,
                        IMeetupEventCrawler meetupEventCrawler,
                        IFileEventCrawler fileEventCrawler,
                        IOptions<AppSettings> options)
    {
        ArgumentNullException.ThrowIfNull(cardBuilder);
        ArgumentNullException.ThrowIfNull(meetupEventCrawler);
        ArgumentNullException.ThrowIfNull(fileEventCrawler);
        ArgumentNullException.ThrowIfNull(options);

        _cardBuilder = cardBuilder;
        _meetupEventCrawler = meetupEventCrawler;
        _fileEventCrawler = fileEventCrawler;
        appSettings = options.Value;
    }

    /// <summary>
    /// Reads the JSON from <paramref name="jsonPath"/> and retuns an object of type <see cref="CardModelBase"/>.
    /// </summary>
    /// <param name="jsonPath">The path to a valid JSON.</param>
    /// <returns>A <see cref="CardModelBase"/> with the data from the JSON file.</returns>
    private static CardModelBase GetCardModelData(string jsonPath)
    {
        ExceptionHelpers.ThrowIfPathNotExists(jsonPath);

        var json = File.ReadAllText(jsonPath);
        var data = JsonConvert.DeserializeObject<CardModelBase>(json);
        ExceptionHelpers.ThrowIfNull(data);

        data.Validate();

        return data;
    }

    /// <inheritdoc/>
    public async Task ProcessCardAsync(string directory)
    {
        ExceptionHelpers.ThrowIfPathNotExists(directory);

        var jsonFileContent = Path.Combine(directory, Consts.CardJsonFilename);
        var cardDataBase = GetCardModelData(jsonFileContent);

        //Find the correct model for this card.
        switch (cardDataBase.TemplateDataModel)
        {
            case nameof(CardSearchModel):
                _cardBuilder.AddCard(CardSearchModel.FromBase(cardDataBase));
                break;
            case nameof(CardImageModel):
                _cardBuilder.AddCard(CardImageModel.FromBase(cardDataBase));
                break;
            case nameof(CardCalendarEventsModel):
                var calendarCard = CardCalendarEventsModel.FromBase(cardDataBase);
                calendarCard.CalendarEvents = await this.GetCalendarEvents();
                _cardBuilder.AddCard(calendarCard);
                break;
            case nameof(CardArticleModel):
                throw new Exception($"Method {nameof(ProcessCardAsync)} cannot be used with {nameof(CardArticleModel)}, use {nameof(ProcessArticleCard)} instead.");
            default:
                throw new Exception($"{cardDataBase.TemplateDataModel} switch is missing.");
        };

        //copy all media associated with this card
        if (Directory.Exists(Path.Combine(directory, "media")))
        {
            Helpers.Copy(
                    Path.Combine(directory, Consts.MediaFolderName),
                    Path.Combine(appSettings.OutputFolderPath, Consts.MediaFolderName)
            );

            //create smaller versions of the media
            foreach (var file in Directory.GetFiles(Path.Combine(directory, Consts.MediaFolderName)))
            {
                var ext = Path.GetExtension(file);
                var name = Path.GetFileNameWithoutExtension(file);
                Helpers.ResizeImage(file,
                    Path.Combine(appSettings.OutputFolderPath, Consts.MediaFolderName, name + "-small" + ext),
                    new Size(300, 10000)
                );//stop at 300 width, who cares about height 
            }
        }
    }

    /// <summary>
    /// Retrieves all calendar events from meetup.com but also from a file repo located at <see cref="Consts.WorkingEventsFolderName"/>.
    /// </summary>
    /// <returns>A list of <see cref="CalendarEvent"/>.</returns>
    private async Task<IList<CalendarEvent>> GetCalendarEvents()
    {
        List<CalendarEvent> calendarEvents = new();

        if( !string.IsNullOrWhiteSpace(appSettings.MeetupUserGroupName))
        {
            ExceptionHelpers.ThrowIfNullOrWhiteSpace(appSettings.MeetupUserGroupIcalUrl);
            ExceptionHelpers.ThrowIfNullOrWhiteSpace(appSettings.MeetupUserGroupUrl);

            calendarEvents.AddRange(
                await _meetupEventCrawler.GetAsync(appSettings.MeetupUserGroupName,
                                                    new Uri(appSettings.MeetupUserGroupUrl),
                                                    new Uri(appSettings.MeetupUserGroupIcalUrl))
            );
        }

        calendarEvents.AddRange(
            _fileEventCrawler.Get(
                Path.Combine(appSettings.WorkingFolderPath, Consts.WorkingEventsFolderName)
            )
        );

        return calendarEvents;
    }

    /// <inheritdoc/>
    public IEnumerable<string> GetRightColumnCardsHtml()
    {
        return _cardBuilder.GetRightColumnCardsHtml();
    }

    /// <inheritdoc/>
    public void ProcessArticleCard(CardArticleModel cardData, DateTime datePublished)
    {
        ArgumentNullException.ThrowIfNull(cardData);
        _cardBuilder.AddArticleCard(cardData, datePublished);
    }

    /// <inheritdoc/>
    public IEnumerable<string> GetBodyCardsHtml(int pageIndex, int cardsPerPage)
    {
        return _cardBuilder.GetBodyCardsHtml(pageIndex, cardsPerPage);
    }

    /// <inheritdoc/>
    public int GetCardsNumber(int cardsPerPage)
    {
        return _cardBuilder.GetCardsNumber(cardsPerPage);
    }

    public void Dispose()
    {
        _cardBuilder.Dispose();

    }
}