﻿using Blog.Builder.Exceptions;
using Blog.Builder.Models.Templates;
using Blog.Builder.Services.Interfaces;
using Blog.Builder.Services.Interfaces.Builders;
using SixLabors.ImageSharp;
using WebMarkupMin.Core;

namespace Blog.Builder.Services;

/// <inheritdoc/>
internal class PageProcessor : IPageProcessor
{
    private readonly IPathService _pathService;
    private readonly ISitemapBuilder _sitemapBuilder;
    private readonly IMarkupMinifier _markupMinifier;
    private readonly IPageBuilder _pageBuilder;
    private readonly ICardProcessor _cardPreparation;


    public PageProcessor(IPathService pathService,
                        ISitemapBuilder sitemapBuilder,
                        IMarkupMinifier markupMinifier,
                        IPageBuilder pageBuilder,
                        ICardProcessor cardPreparation)
    {
        ArgumentNullException.ThrowIfNull(pathService);
        ArgumentNullException.ThrowIfNull(sitemapBuilder);
        ArgumentNullException.ThrowIfNull(markupMinifier);
        ArgumentNullException.ThrowIfNull(pageBuilder);
        ArgumentNullException.ThrowIfNull(cardPreparation);

        _pathService = pathService;
        _sitemapBuilder = sitemapBuilder;
        _markupMinifier = markupMinifier;
        _pageBuilder = pageBuilder;
        _cardPreparation = cardPreparation;
    }

    /// <inheritdoc/>
    public void ProcessPage<T>(string directory) where T : LayoutModelBase
    {
        ExceptionHelpers.ThrowIfPathNotExists(directory);

        //get the result from the builder
        var builderResult = _pageBuilder.Build<T>(directory);

        //minify and save page
        var minifier = _markupMinifier.Minify(builderResult.FinalHtml);
        if (minifier.Errors.Count() > 0)
        {
            throw new Exception($"Minification failed with at least one error : {minifier.Errors.First().Message}");
        }
        ExceptionHelpers.ThrowIfNullOrWhiteSpace(minifier.MinifiedContent);
        var savingPath = Path.Combine(_pathService.OutputFolder, Path.GetFileName(builderResult.RelativeUrl));
        File.WriteAllText(savingPath, minifier.MinifiedContent);

        //copy all media associated with this page
        if (Directory.Exists(Path.Combine(directory, "media")))
        {
            Helpers.Copy(Path.Combine(directory, "media"), _pathService.OutputMediaFolder);
            foreach (var file in Directory.GetFiles(Path.Combine(directory, "media")))
            {
                var ext = Path.GetExtension(file);
                var name = Path.GetFileNameWithoutExtension(file);
                Helpers.ResizeImage(file, Path.Combine(_pathService.OutputMediaFolder, name + "-small" + ext), new Size(500, 10000));//stop at 500 width, who cares about height
            }
        }

        _sitemapBuilder.Add(builderResult.RelativeUrl, builderResult.DateModified);
    }

    /// <inheritdoc/>
    public void ProcessIndex(LayoutIndexModel pageData, int cardsPerPage)
    {
        ArgumentNullException.ThrowIfNull(pageData);

        //get the html for the cards and validate the data
        pageData.Body = _cardPreparation.GetHtml(pageData.Paging.CurrentPageIndex, cardsPerPage);
        pageData.Validate();

        //get the entire page html and minify it
        var builderResult = _pageBuilder.Build(pageData);
        var minifier = _markupMinifier.Minify(builderResult.FinalHtml);
        if (minifier.Errors.Count() > 0)
        {
            throw new Exception($"Minification failed with at least one error : {minifier.Errors.First().Message}");
        }
        ExceptionHelpers.ThrowIfNullOrWhiteSpace(minifier.MinifiedContent);

        //save it
        var indexPageNumber = pageData.Paging.CurrentPageIndex == 0 ? string.Empty : "-page-" + (pageData.Paging.CurrentPageIndex + 1);
        var savingPath = Path.Combine(_pathService.OutputFolder, $"index{indexPageNumber}.html");
        File.WriteAllText(savingPath, minifier.MinifiedContent);

        //add it to sitemap.xml
        _sitemapBuilder.Add(savingPath, builderResult.DateModified);
    }
}
