﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Text;
using System.Web.Security;
using System.IO;
using Roadkill.Core.Diff;
using Roadkill.Core.Converters;
using Roadkill.Core.Domain;

namespace Roadkill.Core.Controllers
{
	/// <summary>
	/// Provides all page related functionality, including editing and viewing pages.
	/// </summary>
	[HandleError]
	[OptionalAuthorization]
	public class PagesController : ControllerBase
	{
		public PagesController() : this(new ServiceContainer()) {}
		public PagesController(IServiceContainer container) : base(container) { }

		/// <summary>
		/// Displays all pages in Roadkill.
		/// </summary>
		/// <returns>An <see cref="IEnumerable`PageSummary"/> as the model.</returns>
		public ActionResult AllPages()
		{
			return View(ServiceContainer.PageManager.AllPages());
		}

		/// <summary>
		/// Displays all tags (categories if you prefer that term) in Roadkill.
		/// </summary>
		/// <returns>An <see cref="IEnumerable`TagSummary"/> as the model.</returns>
		public ActionResult AllTags()
		{
			return View(ServiceContainer.PageManager.AllTags());
		}

		/// <summary>
		/// Returns all tags in the system as a JSON string.
		/// </summary>
		/// <returns>A string array of tags.</returns>
		/// <remarks>This action requires editor rights.</remarks>
		[EditorRequired]
		public ActionResult AllTagsAsJson()
		{
			IEnumerable<TagSummary> tags = ServiceContainer.PageManager.AllTags();
			List<string> tagsArray = new List<string>();
			foreach (TagSummary summary in tags)
			{
				tagsArray.Add(summary.Name);
			}

			return Json(tagsArray, JsonRequestBehavior.AllowGet);
		}

		/// <summary>
		/// Displays all pages for a particular user.
		/// </summary>
		/// <param name="id">The username</param>
		/// <param name="encoded">Whether the username paramter is Base64 encoded.</param>
		/// <returns>An <see cref="IEnumerable`PageSummary"/> as the model.</returns>
		public ActionResult ByUser(string id,bool? encoded)
		{
			// Usernames are base64 encoded by roadkill (to cater for usernames like domain\john).
			// However the URL also supports humanly-readable format, e.g. /ByUser/chris
			if (encoded == true)
			{
				id = id.FromBase64();
			}

			ViewData["Username"] = id;

			return View(ServiceContainer.PageManager.AllPagesCreatedBy(id));
		}

		/// <summary>
		/// Deletes a wiki page.
		/// </summary>
		/// <param name="id">The id of the page to delete.</param>
		/// <returns>Redirects to AllPages action.</returns>
		/// <remarks>This action requires admin rights.</remarks>
		[AdminRequired]
		public ActionResult Delete(int id)
		{
			ServiceContainer.PageManager.DeletePage(id);

			return RedirectToAction("AllPages");
		}

		/// <summary>
		/// Displays the edit View for the page provided in the id.
		/// </summary>
		/// <param name="id">The ID of the page to edit.</param>
		/// <returns>An filled <see cref="PageSummary"/> as the model. If the page id cannot be found, the action
		/// redirects to the New page.</returns>
		/// <remarks>This action requires editor rights.</remarks>
		[EditorRequired]
		public ActionResult Edit(int id)
		{
			PageSummary summary = ServiceContainer.PageManager.GetById(id);

			if (summary != null)
			{
				if (summary.IsLocked && !RoadkillContext.Current.IsAdmin)
					return new HttpStatusCodeResult(403, string.Format("The page '{0}' can only be edited by administrators.",summary.Title));

				return View("Edit", summary);
			}
			else
			{
				return RedirectToAction("New");
			}
		}

		/// <summary>
		/// Saves all POST'd data for a page edit to the database.
		/// </summary>
		/// <param name="summary">A filled <see cref="PageSummary"/> containing the new data.</param>
		/// <returns>Redirects to /Wiki/{id} using the passed in <see cref="PageSummary.Id"/>.</returns>
		/// <remarks>This action requires editor rights.</remarks>
		[EditorRequired]
		[HttpPost]
		[ValidateInput(false)]
		public ActionResult Edit(PageSummary summary)
		{
			if (!ModelState.IsValid)
				return View("Edit", summary);

			ServiceContainer.PageManager.UpdatePage(summary);

			return RedirectToAction("Index", "Wiki", new { id = summary.Id , nocache = DateTime.Now.Ticks });
		}

		/// <summary>
		/// This action is for JSON calls only. Displays a HTML preview for the provided 
		/// wiki markup/markdown. This action is POST only.
		/// </summary>
		/// <param name="id">The wiki markup.</param>
		/// <returns>The markup as rendered as HTML.</returns>
		/// <remarks>This action requires editor rights.</remarks>
		[ValidateInput(false)]
		[EditorRequired]
		[HttpPost]
		public ActionResult GetPreview(string id)
		{
			string html = "";

			if (!string.IsNullOrEmpty(id))
			{
				html = id.WikiMarkupToHtml();
			}

			return JavaScript(html);
		}		

		/// <summary>
		/// Lists the history of edits for a page.
		/// </summary>
		/// <param name="id">The ID of the page.</param>
		/// <returns>An <see cref="IList`HistorySummary"/> as the model.</returns>
		public ActionResult History(int id)
		{
			return View(ServiceContainer.HistoryManager.GetHistory(id).ToList());
		}

		/// <summary>
		/// Displays the Edit view in new page mode.
		/// </summary>
		/// <returns>An empty <see cref="PageSummary"/> as the model.</returns>
		/// <remarks>This action requires editor rights.</remarks>
		[EditorRequired]
		public ActionResult New(string title = "")
		{
			return View("Edit", new PageSummary(){Title = title});
		}

		/// <summary>
		/// Saves a new page using the provided <see cref="PageSummary"/> object to the database.
		/// </summary>
		/// <param name="summary">The page details to save.</param>
		/// <returns>Redirects to /Wiki/{id} using the newly created page's ID.</returns>
		/// <remarks>This action requires editor rights.</remarks>
		[EditorRequired]
		[HttpPost]
		[ValidateInput(false)]
		public ActionResult New(PageSummary summary)
		{
			if (!ModelState.IsValid)
				return View("Edit", summary);

			summary = ServiceContainer.PageManager.AddPage(summary);

			return RedirectToAction("Index", "Wiki", new { id = summary.Id, nocache = DateTime.Now.Ticks });
		}

		/// <summary>
		/// Reverts a page to the version specified, creating a new version in the process.
		/// </summary>
		/// <param name="versionId">The Guid ID of the version to revert to.</param>
		/// <param name="pageId">The id of the page</param>
		/// <returns>Redirects to the History action using the pageId parameter.</returns>
		/// <remarks>This action requires editor rights.</remarks>
		[EditorRequired]
		public ActionResult Revert(Guid versionId, int pageId)
		{
			// Check if the page is locked to admin edits only before reverting.
			PageSummary page = ServiceContainer.PageManager.GetById(pageId);
			if (page == null || (page.IsLocked && !RoadkillContext.Current.IsAdmin))
				return RedirectToAction("Index", "Home");

			ServiceContainer.HistoryManager.RevertTo(versionId);

			return RedirectToAction("History", new { id = pageId });
		}		

		/// <summary>
		/// Returns all pages for the given tag.
		/// </summary>
		/// <param name="id">The tag name</param>
		/// <returns>An <see cref="IEnumerable`PageSummary"/> as the model.</returns>
		public ActionResult Tag(string id)
		{
			ViewData["Tagname"] = id;

			return View(ServiceContainer.PageManager.FindByTag(id));
		}

		/// <summary>
		/// Gets a particular version of a page.
		/// </summary>
		/// <param name="id">The Guid ID for the version.</param>
		/// <returns>A <see cref="PageSummary"/> as the model, which contains the HTML diff
		/// output inside the <see cref="PageSummary.Content"/> property.</returns>
		public ActionResult Version(Guid id)
		{
			IList<PageSummary> bothVersions = ServiceContainer.HistoryManager.CompareVersions(id).ToList();
			string diffHtml = "";

			if (bothVersions[1] != null)
			{
				string oldVersion = bothVersions[1].Content.WikiMarkupToHtml();
				string newVersion = bothVersions[0].Content.WikiMarkupToHtml();
				HtmlDiff diff = new HtmlDiff(oldVersion, newVersion);
				diffHtml = diff.Build();
			}
			else
			{
				diffHtml = bothVersions[0].Content.WikiMarkupToHtml();
			}

			PageSummary summary = bothVersions[0];
			summary.Content = diffHtml;
			return View(summary);
		}
	}
}
