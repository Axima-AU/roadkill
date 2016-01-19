﻿using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Web.Mvc;
using NUnit.Framework;
using Roadkill.Core;
using Roadkill.Core.AmazingConfig;
using Roadkill.Core.Cache;
using Roadkill.Core.Mvc.Controllers;
using Roadkill.Core.Mvc.ViewModels;
using Roadkill.Tests.Unit.StubsAndMocks;

namespace Roadkill.Tests.Unit.Mvc.Controllers.Admin
{
	[TestFixture]
	[Category("Unit")]
	public class CacheControllerTests
	{
		private MocksAndStubsContainer _container;

		private IConfigurationStore _configurationStore;
		private IConfiguration _configuration;

		private IUserContext _context;
		private UserServiceMock _userService;
		private PageViewModelCache _pageCache;
		private ListCache _listCache;
		private SiteCache _siteCache;
		private MemoryCache _cache;

		private CacheController _cacheController;

		[SetUp]
		public void Setup()
		{
			_container = new MocksAndStubsContainer();
			_container.ClearCache();

			_configurationStore = _container.ConfigurationStoreMock;
			_configuration = _configurationStore.Load();

			_context = _container.UserContext;
			_userService = _container.UserService;
			_pageCache = _container.PageViewModelCache;
			_listCache = _container.ListCache;
			_siteCache = _container.SiteCache;
			_cache = _container.MemoryCache;

			_cacheController = new CacheController(_configurationStore, _userService, _context, _listCache, _pageCache, _siteCache);
		}

		[Test]
		public void index_should_return_viewmodel_with_filled_properties()
		{
			// Arrange
			_configuration.UseObjectCache = true;			
			_pageCache.Add(1, new PageViewModel());
			_listCache.Add<string>("test", new List<string>());
			_siteCache.AddMenu("menu");

			// Act
			ViewResult result = _cacheController.Index() as ViewResult;

			// Assert
			Assert.That(result, Is.Not.Null, "ViewResult");

			CacheViewModel model = result.ModelFromActionResult<CacheViewModel>();
			Assert.NotNull(model, "Null model");
			Assert.That(model.IsCacheEnabled, Is.True);
			Assert.That(model.PageKeys.Count(), Is.EqualTo(1));
			Assert.That(model.ListKeys.Count(), Is.EqualTo(1));
			Assert.That(model.SiteKeys.Count(), Is.EqualTo(1));
		}

		[Test]
		public void clear_should_redirect_and_clear_all_cache_items()
		{
			// Arrange
			_configuration.UseObjectCache = true;
			_pageCache.Add(1, new PageViewModel());
			_listCache.Add<string>("test", new List<string>());
			_siteCache.AddMenu("menu");

			// Act
			RedirectToRouteResult result = _cacheController.Clear() as RedirectToRouteResult;

			// Assert
			Assert.That(result, Is.Not.Null, "RedirectToRouteResult");
			Assert.That(result.RouteValues["action"], Is.EqualTo("Index"));
			Assert.That(_cacheController.TempData["CacheCleared"], Is.EqualTo(true));

			Assert.That(_cache.Count(), Is.EqualTo(0));
		}
	}
}
