﻿using System.Web;
using System.Web.Mvc;
using Roadkill.Core.AmazingConfig;
using Roadkill.Core.Mvc.Attributes;
using Roadkill.Core.Plugins;
using Roadkill.Core.Security;

namespace Roadkill.Core.Mvc.Controllers
{
	/// <summary>
	/// Provides functionality for /Special: urls
	/// </summary>
	[OptionalAuthorization]
	public class SpecialPagesController : ControllerBase
	{
		private readonly IPluginFactory _pluginFactory;

		public SpecialPagesController(IConfigurationStore configurationStore, UserServiceBase userManager, 
			IUserContext context, IPluginFactory pluginFactory)
			: base(configurationStore, userManager, context) 
		{
			_pluginFactory = pluginFactory;
		}

		/// <summary>
		/// Calls any special page plugin based on the id (the name).
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		/// <exception cref="HttpException">Thrown when a special page plugin cannot be found for id/name.</exception>
		public ActionResult Index(string id)
		{
			SpecialPagePlugin plugin = _pluginFactory.GetSpecialPagePlugin(id);

			// Throw an HttpException so the customerrors is used and not the default asp.net 404 page
			if (plugin == null)
				throw new HttpException(404, string.Format("A plugin for the special page '{0}' was not found", id));

			return plugin.GetResult(this);
		}
	}
}