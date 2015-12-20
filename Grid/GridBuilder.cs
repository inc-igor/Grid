﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Web.Routing;
using System.Web.UI;
using System.Web.WebPages;
using Grid.Attributes;
using Grid.GridParts;
using Grid.Interfaces;
using Grid.Options;
using Grid.Paging;
using Incoding.Extensions;
using Incoding.MvcContrib;
using Grid.Components;

namespace Grid
{
    public class GridBuilder<T> : IGridBuilder<T>, IGridBuilderOptions<T>, IHtmlString where T : class
    {
        #region Constructors

        public GridBuilder(HtmlHelper htmlHelper)
        {
            this._htmlHelper = htmlHelper;
        }

        #endregion

        #region Fields

        readonly HtmlHelper _htmlHelper;
        
        string _noRecordsTemplate, _ajaxGetAction, _gridClass, _templateId, _contentTable, _pagingTemplateId, _noRecords, _sortBySelector, _descSelector, _pagingContainer, _pageSizesSelect;

        bool _isPageable, _isScrolling, _showItemsCount, _showPageSizes;

        int[] _pageSizesArray;

        int _buttonCount = 5, _contentTableHeight;

        IDictionary<string, object> RowAttributes, HeaderRowAttributes;

        Func<ITemplateSyntax<T>, HelperResult> RowTemplateClass;

        readonly List<Column<T>> _сolumnList = new List<Column<T>>();

        readonly List<Row<T>> _nextRowList = new List<Row<T>>();

        Selector _customPagingTemplate;

        Action<IIncodingMetaLanguageCallbackBodyDsl> _onBindAction = dsl => { };

        JqueryBind _onJqueryBind;



        #endregion

        #region Api Methods

        public IGridBuilderOptions<T> Id(string gridId)
        {
            Func<string, string> SetName = (part) => "{0}-{1}".F(gridId, part);
            
            this._contentTable = gridId;
            this._templateId = SetName("template");
            this._pagingTemplateId = SetName("pagingTemplateId");           
            this._noRecords = SetName("noRecords");
            this._sortBySelector = SetName("sortBySelector");
            this._descSelector = SetName("descSelector");
            this._pagingContainer = SetName("pagingContainer");
            this._pageSizesSelect = SetName("pageSizesSelect");

            return this;
        }

        public IGridBuilderOptions<T> Styling(string @class)
        {
            this._gridClass = @class;
            return this;
        }

        public IGridBuilderOptions<T> Styling(BootstrapTable @class)
        {
            var classes = Enum.GetValues(typeof(BootstrapTable))
                .Cast<BootstrapTable>()
                .Where(r => @class.HasFlag(r))
                .Select(r => r.ToLocalization())
                .AsString(" ");
            return Styling(classes);
        }

        public IGridBuilderOptions<T> Columns(Action<ColumnsBuilder<T>> builderAction)
        {
            builderAction(new ColumnsBuilder<T>(this));
            return this;
        }

        public IGridBuilderOptions<T> NextRow(Func<ITemplateSyntax<T>, HelperResult> content)
        {
            this._nextRowList.Add(new Row<T>(content));
            return this;
        }

        public IGridBuilderOptions<T> NextRow(Action<Row<T>> action)
        {
            Row<T> row = new Row<T>();
            action(row);
            this._nextRowList.Add(row);
            return this;
        }

        public IGridBuilderOptions<T> Scrolling(int height)
        {
            this._isScrolling = true;
            this._contentTableHeight = height;
            return this;
        }

        //for tbody tr
        
        public IGridBuilderOptions<T> RowAttr(RouteValueDictionary htmlAttributes)
        {
            this.RowAttributes = htmlAttributes;
            return this;
        }

        public IGridBuilderOptions<T> RowAttr(object htmlAttributes)
        {
            return RowAttr(AnonymousHelper.ToDictionary(htmlAttributes));
        }

        public IGridBuilderOptions<T> RowClass(Func<ITemplateSyntax<T>, HelperResult> template)
        {
            this.RowTemplateClass = template;
            return this;
        }


        public IGridBuilderOptions<T> RowClass(B htmlClass)
        {
            return RowAttr(AnonymousHelper.ToDictionary(new { @class = htmlClass.ToLocalization() }));
        }

        public IGridBuilderOptions<T> RowClass(string htmlClass)
        {
            return RowAttr(AnonymousHelper.ToDictionary(new { @class = htmlClass }));
        }

        //end for tbody tr


        //for thead tr
        public IGridBuilderOptions<T> HeadAttr(RouteValueDictionary htmlAttributes)
        {
            this.HeaderRowAttributes = htmlAttributes;
            return this;
        }

        public IGridBuilderOptions<T> HeadAttr(object htmlAttributes)
        {
            return HeadAttr(AnonymousHelper.ToDictionary(htmlAttributes));
        }

        public IGridBuilderOptions<T> HeadClass(B htmlClass)
        {
            return HeadAttr(AnonymousHelper.ToDictionary(new { @class = htmlClass.ToLocalization() }));
        }

        public IGridBuilderOptions<T> HeadClass(string htmlClass)
        {
            return HeadAttr(AnonymousHelper.ToDictionary(new { @class = htmlClass }));
        }

        //end for thead tr

        public IGridBuilderOptions<T> AjaxGet(string actionString)
        {
            this._ajaxGetAction = actionString.AppendToQueryString(new
                                                     {
                                                         Desc = Selector.Jquery.Name(this._descSelector),
                                                         SortBy = Selector.Jquery.Name(this._sortBySelector)
                                                     });
            return this;
        }

        public IGridBuilderOptions<T> Sortable()
        {
            if (_сolumnList.Any())
            {
                foreach (var column in _сolumnList)
                    column.SortBy = column.Expression;

                _сolumnList.First().SortDefault = true;
            }
            return this;
        }

        public IGridBuilderOptions<T> OnBind(Action<IIncodingMetaLanguageCallbackBodyDsl> action, JqueryBind jqueryBind = JqueryBind.InitIncoding)
        {
            this._onBindAction = action;
            this._onJqueryBind = jqueryBind;
            return this;
        }

        public IGridBuilderOptions<T> NoRecords(string text)
        {
            var div = new TagBuilder("div");
            div.GenerateId(this._noRecords);
            div.Attributes.Add("style", "display: none;");
            var table = new TagBuilder("table");
            var tbody = new TagBuilder("tbody");
            var tr = new TagBuilder("tr");
            var td = new TagBuilder("td");
            td.InnerHtml = text;
            tr.InnerHtml = td.ToString();
            tbody.InnerHtml = tr.ToString();
            table.InnerHtml = tbody.ToString();
            div.InnerHtml = table.ToString();

            this._noRecordsTemplate = div.ToString();
            return this;
        }

        public IGridBuilderOptions<T> Pageable()
        {
            this._isPageable = true;
            return this;
        }

        public IGridBuilderOptions<T> Pageable(Selector customPagingTemplate)
        {
            this._customPagingTemplate = customPagingTemplate;
            return Pageable();
        }

        public IGridBuilderOptions<T> Pageable(Action<PageableBuilder<T>> builderAction)
        {
            builderAction(new PageableBuilder<T>(this));
            return Pageable();
        }

        #endregion


        #region PrivateMethods

        MvcHtmlString Render()
        {
            var divMain = new TagBuilder("div");
            divMain.AddCssClass("inc-grid");

            var table = new TagBuilder("table");
            table.AddCssClass(string.IsNullOrWhiteSpace(this._gridClass) ? GridOptions.Default.GetStyling() : this._gridClass);
            table.AddCssClass("table");


            var thead = new TagBuilder("thead");
            var tr = new TagBuilder("tr");

            if (!_сolumnList.Any())
                AutoBind();

            foreach (var column in this._сolumnList)
                tr.InnerHtml += AddTh(column);

            thead.InnerHtml = tr.ToString();
            table.InnerHtml = thead.ToString();
            divMain.InnerHtml = table.ToString();

            if (_isPageable)
                divMain.InnerHtml += AddPageableTemplate();
            else
                divMain.InnerHtml += AddTemplate();

            divMain.InnerHtml += this._noRecordsTemplate ?? this.NoRecordsDefault();

            if (this._сolumnList.Any(r => r.SortBy != null))
            {
                divMain.InnerHtml += this._htmlHelper.Hidden(this._sortBySelector, "");
                divMain.InnerHtml += this._htmlHelper.CheckBox(this._descSelector, true, new { style = "display: none;" });
            }

            return new MvcHtmlString(divMain.ToString());
        }

        MvcHtmlString AddTh(Column<T> column)
        {
            var attributes = column.ColumnHeaderAttributes ?? column.ColumnAttributes;
            var thWidth = String.IsNullOrWhiteSpace(column.ColumnWidthPct) ? column.ColumnWidth : column.ColumnWidthPct;

            var th = new TagBuilder("th");

            if (column.SortBy != null)
            {
                var link = SortArrow(setting =>
                {
                    setting.Content = column.Name;
                    setting.TargetId = this._contentTable;
                    setting.By = column.SortBy;
                    setting.SortDefault = column.SortDefault;
                });

                th.InnerHtml = link.ToString();
            }
            else
                th.InnerHtml = column.Name;

            //thead tr attributes first
            if (this.HeaderRowAttributes != null)
                th.MergeAttributes(this.HeaderRowAttributes);

            //unique th attributes
            if (attributes != null)
                th.MergeAttributes(attributes, true);

            if (!String.IsNullOrWhiteSpace(thWidth) && (attributes == null || !attributes.ContainsKey("style")))
                th.MergeAttribute("style", "width:{0}{1};".F(thWidth, String.IsNullOrWhiteSpace(column.ColumnWidthPct) ? "px" : "%"));

            if (!column.IsVisible)
                th.AddCssClass("hide");

            return new MvcHtmlString(th.ToString());
        }

        MvcHtmlString AddTemplate()
        {

            var table = this._htmlHelper.When(GridOptions.Default.InitBind)
                    .DoWithPreventDefaultAndStopPropagation()
                    .AjaxGet(this._ajaxGetAction)
                    .OnSuccess(dsl =>
                    {                        
                        dsl.Self().Core().Insert.WithTemplate(Selector.Jquery.Id(this._templateId)).Html();

                        dsl.Self().Core().JQuery.Manipulation.Html(Selector.Jquery.Id(this._noRecords).Text())
                                .If(r => r.Data<List<T>>(data => data.IsEmpty()));
                    })
                    .When(_onJqueryBind)
                    .OnSuccess(_onBindAction)
                    .OnError(dsl => dsl.Self().Core().JQuery.Manipulation.Html("Error ajax get"))
                    .AsHtmlAttributes(new { id = this._contentTable, @class = "table " + (string.IsNullOrWhiteSpace(this._gridClass) ? GridOptions.Default.GetStyling() : this._gridClass) })
                    .ToTag(HtmlTag.Table);

            var divContent = new TagBuilder("div");
            divContent.AddCssClass("content-table");
            if(this._isScrolling)
                divContent.MergeAttribute("style", "height: {0}px; overflow: auto;".F(this._contentTableHeight));
            divContent.InnerHtml = table.ToString();

            return new MvcHtmlString("{0}{1}".F(divContent, CreateTemplate()));
        }

        MvcHtmlString AddPageableTemplate()
        {
            var tableWithPageable = this._htmlHelper.When(GridOptions.Default.InitBind | JqueryBind.IncChangeUrl)
                    .DoWithPreventDefaultAndStopPropagation()
                    .AjaxHashGet(this._ajaxGetAction)
                    .OnSuccess(dsl =>
                    {
                        dsl.Self().Core().Insert.For<PagingResult<T>>(result => result.Items)
                                .WithTemplate(Selector.Jquery.Id(this._templateId)).Html();

                        dsl.With(selector => selector.Id(_pagingContainer)).Core().Insert.For<PagingResult<T>>(result => result.Paging)
                                .WithTemplate(_customPagingTemplate ?? Selector.Jquery.Id(this._pagingTemplateId)).Html();

                        dsl.Self().Core().JQuery.Manipulation.Html(Selector.Jquery.Id(this._noRecords).Text())
                                .If(r => r.Data<List<T>>(data => data.IsEmpty()));

                        if (_showItemsCount)
                            dsl.With(selector => selector.Id(_pagingContainer)).Core().Insert.For<PagingResult<T>>(result => result.PagingRange).Append();
                    })
                    .When(_onJqueryBind)
                    .OnSuccess(_onBindAction)
                    .OnError(dsl => dsl.Self().Core().JQuery.Manipulation.Html("Error ajax get"))
                    .AsHtmlAttributes(new { id = this._contentTable, @class = "table " + (string.IsNullOrWhiteSpace(this._gridClass) ? GridOptions.Default.GetStyling() : this._gridClass) })
                    .ToTag(HtmlTag.Table);

            var divContent = new TagBuilder("div");
            divContent.AddCssClass("content-table");
            divContent.InnerHtml = tableWithPageable.ToString();

            var divPagingContainer = new TagBuilder("div");
            divPagingContainer.GenerateId(_pagingContainer);
            divPagingContainer.AddCssClass("pagination");

            var selectPageSizes = this._htmlHelper.DropDownList("PageSize", new SelectList(_pageSizesArray ?? new int[] { 5, 10, 50, 100 }), null,
                _htmlHelper.When(JqueryBind.Change)
                    .Direct()
                    .OnSuccess(dsl =>
                    {
                        dsl.Self().Core().Store.Hash.Insert();
                        dsl.Self().Core().Store.Hash.Manipulate(manipulateDsl => manipulateDsl.Set("Page", 1));
                    })
                    .AsHtmlAttributes(new { style = "width: 50px;", id = _pageSizesSelect }));

            return new MvcHtmlString("{0}{1}{2}{3}".F(divContent, 
                                                      CreateTemplate(), 
                                                      divPagingContainer.ToString(), 
                                                      _showPageSizes ? selectPageSizes.ToString() : ""));
        }

        private StringBuilder CreateTemplate()
        {
            var sb = new StringBuilder();
            using (TextWriter writer = new StringWriter(sb))
            {
                var writerHtmlHelper = new HtmlHelper<T>(new ViewContext
                {
                    Writer = new HtmlTextWriter(writer)
                }, new ViewPage());

                //create template
                using (var template = writerHtmlHelper.Incoding().ScriptTemplate<T>(this._templateId))
                {
                    using (var each = template.ForEach())
                    {
                        var tbody = new TagBuilder("tbody");
                        var tr = new TagBuilder("tr");

                        if (this.RowAttributes != null)
                            tr.MergeAttributes(this.RowAttributes);

                        if (this.RowTemplateClass != null)
                            tr.AddCssClass(this.RowTemplateClass.Invoke(each).ToString());

                        foreach (var column in this._сolumnList)
                        {
                            var tdWidth = String.IsNullOrWhiteSpace(column.ColumnWidthPct) ? column.ColumnWidth : column.ColumnWidthPct;
                            var td = new TagBuilder("td");

                            if (column.Expression != null)
                            {
                                if (column.IsRaw)
                                    td.InnerHtml = "{{{" + column.Expression + "}}}";
                                else
                                    td.InnerHtml = "{{" + column.Expression + "}}";
                            }

                            if (column.Template != null)
                                td.InnerHtml += column.Template.Invoke(each);

                            if (column.ColumnAttributes != null)
                                td.MergeAttributes(column.ColumnAttributes);

                            if (!String.IsNullOrWhiteSpace(tdWidth) && (column.ColumnAttributes == null || !column.ColumnAttributes.ContainsKey("style")))
                                td.MergeAttribute("style", "width:{0}{1};".F(tdWidth, String.IsNullOrWhiteSpace(column.ColumnWidthPct) ? "px" : "%"));

                            if (!column.IsVisible)
                                td.AddCssClass("hide");

                            tr.InnerHtml += td.ToString();
                        }

                        tbody.InnerHtml += tr.ToString();

                        if (this._nextRowList.Count > 0)
                        {
                            foreach (var row in this._nextRowList)
                            {
                                var trNext = new TagBuilder("tr");
                                
                                if (row.MetaAttribute != null)
                                {
                                    trNext.MergeAttributes(row.MetaAttribute(each).AsHtmlAttributes());
                                }
                                
                                List<string> classes = row.GetClasses(each);
                                foreach (var cls in classes)
                                {
                                    trNext.AddCssClass(cls);
                                }

                                if (this.RowAttributes != null)
                                {
                                    foreach (var att in this.RowAttributes)
                                    {
                                        if (att.Key == "class")
                                            trNext.AddCssClass(att.Value.ToString());
                                    }
                                    trNext.MergeAttributes(this.RowAttributes);
                                }

                                if (this.RowTemplateClass != null)
                                    trNext.AddCssClass(this.RowTemplateClass.Invoke(each).ToString());

                                var innerHtml = row.Content.Invoke(each).ToString();

                                if (row.HtmlAttributes != null)
                                {
                                    innerHtml = innerHtml.Insert(3, " " + row.HtmlAttributes.Invoke(each) + " "); // insert raw html attributes right after "<tr" tag opened
                                }

                                trNext.InnerHtml += innerHtml;
                                tbody.InnerHtml += trNext.ToString();
                            }
                        }
                        sb.Append(tbody);
                    }
                }
                //end create template

                if (_isPageable && _customPagingTemplate == null)
                {
                    using (var template = writerHtmlHelper.Incoding().ScriptTemplate<PagingModel>(this._pagingTemplateId))
                    {
                        sb.Append("<ul class=\"pagination\">");
                        using (var each = template.ForEach())
                        {
                            using (each.Is(r => r.Active))
                            {
                                var li = new TagBuilder("li");
                                var link = new TagBuilder("a");
                                link.MergeAttribute("href", "#!" + each.For(r => r.Url));
                                link.InnerHtml = each.For(r => r.Text);
                                li.InnerHtml = link.ToString();
                                li.AddCssClass("active");
                                sb.Append(li);
                            }
                            using (each.Not(r => r.Active))
                            {
                                var li = new TagBuilder("li");
                                var link = new TagBuilder("a");
                                link.MergeAttribute("href", "#!" + each.For(r => r.Url));
                                link.InnerHtml = each.For(r => r.Text);
                                li.InnerHtml = link.ToString();
                                sb.Append(li);
                            }
                        }
                    }
                    sb.Append("</ul>");
                }
            }
            return sb;
        }

        string NoRecordsDefault()
        {
            NoRecords(GridOptions.Default.NoRecordsText);
            return this._noRecordsTemplate;
        }

        private void AutoBind()
        {
            PropertyInfo[] properties = typeof(T).GetProperties();
            foreach (PropertyInfo propInfo in properties)
            {
                var attrs = propInfo.FirstOrDefaultAttribute<AutoBindAttribute>();
                AddColumn(new Column<T>(propInfo.Name)
                {
                    Expression = propInfo.Name,
                    ColumnWidth = attrs != null ?
                                  attrs.Width == 0 ? String.Empty : attrs.Width.ToString() :
                                  String.Empty,

                    ColumnWidthPct = attrs != null ?
                                     attrs.WidthPct == 0 ? String.Empty : attrs.WidthPct.ToString() :
                                     String.Empty,

                    IsRaw = attrs != null && attrs.Raw,
                    IsVisible = attrs == null || !attrs.Hide,
                    Name = attrs != null ?
                           attrs.Title ?? propInfo.Name :
                           propInfo.Name,

                    SortBy = attrs != null ? attrs.SortBy : null,
                    SortDefault = attrs != null && attrs.SortDefault
                });
            }

        }

        MvcHtmlString SortArrow(Action<SortArrowSetting> action)
        {
            var setting = new SortArrowSetting();
            action(setting);

            var link = this._htmlHelper.When(JqueryBind.Click)
                    .DoWithPreventDefaultAndStopPropagation()
                    .Direct()
                    .OnSuccess(dsl =>
                    {
                        dsl.With(selector => selector.Name(this._sortBySelector)).Core().JQuery.Attributes.Val(setting.By);
                        dsl.With(selector => selector.Name(this._descSelector)).Core().Trigger.Invoke(JqueryBind.Click);
                        dsl.With(selector => selector.Class("sort-arrow")).Core().Trigger.Incoding();
                        dsl.With(selector => selector.Id(setting.TargetId)).Core().Trigger.Incoding();
                    })
                    .AsHtmlAttributes()
                    .ToLink(setting.Content);

            var builder = new StringBuilder();
            builder.Append(link);
            builder.Append(RenderSortArrow(setting.By, true, setting.SortDefault));
            builder.Append(RenderSortArrow(setting.By, false, setting.SortDefault));
            return new MvcHtmlString(builder.ToString());
        }

        MvcHtmlString RenderSortArrow<TEnum>(TEnum sort, bool desc, bool sortDefault)
        {
            string arrowsBootstrap = desc ? "icon-arrow-up" : "icon-arrow-down";

            return this._htmlHelper.When(GridOptions.Default.InitBind)
                    .DoWithStopPropagation().Direct()
                    .OnSuccess(dsl =>
                    {
                        dsl.Self().Core().JQuery.Attributes.AddClass("hide");
                        dsl.Self().Core().JQuery.Attributes.RemoveClass("hide")
                                .If(() => Selector.Jquery.Name(this._sortBySelector) == sort.ToString()
                                        &&
                                        Selector.Jquery.Name(this._descSelector) == desc)
                                ;

                        dsl.Self().Core().JQuery.Attributes.RemoveClass("hide")
                                .If(() => Selector.Jquery.Name(this._sortBySelector) == ""
                                        &&
                                        Selector.Jquery.Name(this._descSelector) == desc
                                        &&
                                        sortDefault == true);

                        dsl.Self().Core().JQuery.Attributes.AddClass("hide")
                                .If(() => Selector.Jquery.Name(this._sortBySelector) == sort.ToString()
                                        &&
                                        Selector.Jquery.Name(this._descSelector) != desc);
                    })
                                .AsHtmlAttributes(new { @class = arrowsBootstrap + " sort-arrow hide" })
                                .ToTag(HtmlTag.I);
        }


        #endregion

        public void AddColumn(Column<T> column)
        {
            this._сolumnList.Add(column);
        }

        public void SetItemsCount(bool showItemsCount)
        {
            _showItemsCount = showItemsCount;
        }

        public void SetPageSizes(bool showPageSizes)
        {
            _showPageSizes = showPageSizes;
        }

        public void SetPageSizesArray(params int[] pageSizesArray)
        {
            _pageSizesArray = pageSizesArray;
        }

        public void SetButtonCount(int count)
        {
            _buttonCount = count;
        }

        public string ToHtmlString()
        {
            var srt = Render().ToString();
            return srt;
        }
    }
}