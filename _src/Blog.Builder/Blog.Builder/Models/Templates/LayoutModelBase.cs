﻿using Blog.Builder.Exceptions;
using Blog.Builder.Models.Templates;
using System.Text.RegularExpressions;

namespace Blog.Builder.Models;

/// <summary>
/// Used for the main template, the layout (template-layout.cshtml)
/// </summary>
public record class LayoutModelBase : ModelBase
{
    public Paging Paging { get; set; } = new Paging();

    public string RelativeUrl { get; set; } = default!;

    public string Title { get; set; } = default!;

    public List<string> Tags { get; set; } = new List<string>();

    public List<string> Section { get; set; } = new List<string>();

    public List<string> ExtraHeaders { get; } = new List<string>();

    public string Description { get; set; } = default!;

    public DateTime DatePublished { get; set; } = default!;

    public DateTime DateModified { get; set; } = default!;

    public DateTime DateExpires { get; } = default!;

    public string Name { get; set; } = default!;

    public string? RelativeImageUrl { get; set; }

    public string Body { get; set; } = default!;

    public string PlainTextDescription
    {
        get
        {
            var result = Regex.Replace(Description, "<.*?>", " ", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline);
            return result.Trim();
        }
    }


    public new void Validate()
    {
        base.Validate();

        ExceptionHelpers.ThrowIfNull(this.DateModified);
        ExceptionHelpers.ThrowIfNull(this.DatePublished);
        ExceptionHelpers.ThrowIfNull(this.DateExpires);
        ExceptionHelpers.ThrowIfNullOrEmpty(this.Tags);
        ExceptionHelpers.ThrowIfNull(this.Section);
        ExceptionHelpers.ThrowIfNull(this.ExtraHeaders);

        ExceptionHelpers.ThrowIfNullOrWhiteSpace(this.Description);
        ExceptionHelpers.ThrowIfNullOrWhiteSpace(this.PlainTextDescription);
        ExceptionHelpers.ThrowIfNullOrWhiteSpace(this.Title);
        ExceptionHelpers.ThrowIfNullOrWhiteSpace(this.RelativeUrl);
        ExceptionHelpers.ThrowIfNullOrWhiteSpace(this.Body);

    }

}
