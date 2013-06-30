using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Hosting;
using System.Web.UI.WebControls;
using EPiServer;
using EPiServer.ClientScript;
using EPiServer.ClientScript.Events;
using EPiServer.Configuration;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.Framework.Configuration;
using EPiServer.ServiceLocation;
using EPiServer.UI.Editor.Tools.Dialogs;
using EPiServer.Web;
using EPiServer.Web.Hosting;

namespace EPiFix.ExternalHomeLinkProperties
{
    public class FixedHyperlinkProperties : HyperlinkProperties
    {
        private ILanguageBranchRepository LanguageBranchRepository
        {
            get;
            set;
        }

        public FixedHyperlinkProperties()
        {
            this.LanguageBranchRepository = ServiceLocator.Current.GetInstance<ILanguageBranchRepository>();
        }

        protected override void OnLoad(EventArgs e)
        {
            this.RegisterClientScriptFile(base.ResolveUrlFromUtil("javascript/common.js"));
            base.Title = this.Translate("/editor/tools/hyperlinkproperties/toolheading");
            if (!base.IsPostBack)
            {
                base.ScriptManager.AddEventListener(this.Page, new CustomEvent(EventType.Load, "Initialize"));
                string text = base.Server.UrlDecode(base.Request.QueryString["url"]) ?? string.Empty;
                string url = text;
                try
                {
                    url = StripEnterpriseHostnames(text);
                }
                catch (UriFormatException arg)
                {
                    // HyperlinkProperties._log.ErrorFormat("Unable to parse url {0}: {1}", text, arg);
                }
                UrlBuilder urlBuilder = null;
                try
                {
                    urlBuilder = new UrlBuilder(url);
                }
                catch (UriFormatException)
                {
                    urlBuilder = new UrlBuilder(string.Empty);
                }
                Global.UrlRewriteProvider.ConvertToInternal(urlBuilder);
                ContentReference contentReference = PermanentLinkUtility.GetContentReference(urlBuilder);
                this.IsInternalUrl = (contentReference != ContentReference.EmptyReference);
                this.IsInternalDocument = ((!this.IsInternalUrl && text.StartsWith("/")) || text.StartsWith("~/"));
                this.IsNetworkDocument = (text.ToLower().StartsWith("file://") || text.StartsWith("\\") ||
                                          text.IndexOf(":") == 1);
                this.IsExternalDocument = (base.Request.QueryString["type"] != null &&
                                           string.Compare(base.Request.QueryString["type"], "doc") == 0);
                this.IsMailLink = text.ToLower().StartsWith("mailto:");
                this.IsUnresolvedInternalLink = (text.ToLower().StartsWith("~/link/") &&
                                                 PageReference.ParseUrl(text) == PageReference.EmptyReference);

                if (this.IsInternalUrl)
                {
                    if (!string.IsNullOrEmpty(urlBuilder.QueryCollection["epslanguage"]))
                    {
                        this.Language = urlBuilder.QueryCollection["epslanguage"];
                    }
                    PageData pageData = null;
                    try
                    {
                        // FIX START: PageReference.ParseUrl(url) changed by PageReference.ParseUrl(urlBuilder.ToString())
                        pageData = this.ContentRepository.Get<PageData>(PageReference.ParseUrl(urlBuilder.ToString()));
                        // FIX END
                    }
                    catch (PageNotFoundException)
                    {
                        this.PageNotFound = true;
                    }
                    catch (AccessDeniedException)
                    {
                        this.PageAccessDenied = true;
                    }
                    if (pageData != null)
                    {
                        this.linkinternalurl.PageLink = pageData.PageLink;
                    }
                }
                else
                {
                    if (this.IsUnresolvedInternalLink)
                    {
                        this.PageNotFound = true;
                        this.linkinternalurl.PageLink = PageReference.EmptyReference;
                    }
                    else
                    {
                        if (this.IsInternalDocument || this.IsNetworkDocument || this.IsExternalDocument)
                        {
                            this.actionTab.SetSelectedTab(1);
                            if (!this.IsInternalDocument)
                            {
                                goto IL_2D8;
                            }
                            try
                            {
                                if (!(HostingEnvironment.VirtualPathProvider.GetFile(urlBuilder.Path) is UnifiedFile))
                                {
                                    this.DocumentNotFound = true;
                                    this.IsInternalDocument = false;
                                    this.actionTab.SetSelectedTab(0);
                                }
                                goto IL_2D8;
                            }
                            catch (System.UnauthorizedAccessException)
                            {
                                this.DocumentUnauthorizedAccess = true;
                                goto IL_2D8;
                            }
                        }
                        if (this.IsMailLink)
                        {
                            this.actionTab.SetSelectedTab(2);
                        }
                    }
                }
            IL_2D8:
                this.PopulateFrameList(this.linkframe);
                this.PopulateFrameList(this.documentframe);
                this.PopulateLanguageList(this.linklanguages);
                this.DataBind();
                return;
            }
            string text2 = "function CloseAfterPostback(e) {";
            if (string.Compare(this.activeTab.Value, "0") == 0 && this.linktypeinternal.Checked)
            {
                UrlBuilder urlBuilder2 = new UrlBuilder(this.ContentRepository.Get<PageData>(this.linkinternalurl.PageLink).StaticLinkURL);
                string value = base.Request.Form[this.linklanguages.UniqueID];
                if (!string.IsNullOrEmpty(value))
                {
                    urlBuilder2.QueryCollection["epslanguage"] = value;
                }
                text2 = text2 + "EPi.GetDialog().returnValue.href = '" + urlBuilder2.ToString() + "';";
            }
            text2 += "EPi.GetDialog().Close(EPi.GetDialog().returnValue);}";
            this.Page.ClientScript.RegisterClientScriptBlock(base.GetType(), "closeafterpostback", text2, true);
            base.ScriptManager.AddEventListener(this.Page, new CustomEvent(EventType.Load, "CloseAfterPostback"));
        }

        private void PopulateFrameList(DropDownList control)
        {
            int num = 1;
            FrameCollection frameCollection = EPiServer.DataAbstraction.Frame.List();
            control.Items.Add(new ListItem(null, null));
            foreach (Frame current in frameCollection)
            {
                control.Items.Add(new ListItem(current.LocalizedDescription, current.Name));
                if (string.Compare(this.Frame, current.Name, true) == 0)
                {
                    control.SelectedIndex = num;
                }
                num++;
            }
        }

        private void PopulateLanguageList(DropDownList control)
        {
            control.Items.Add(new ListItem(this.Translate("/editor/tools/hyperlinkproperties/automatically"), string.Empty));
            if (!Settings.Instance.UIShowGlobalizationUserInterface)
            {
                return;
            }
            foreach (LanguageBranch current in this.LanguageBranchRepository.ListEnabled())
            {
                ListItem listItem = new ListItem(current.Name, current.LanguageID);
                listItem.Selected = (this.Language == current.LanguageID);
                control.Items.Add(listItem);
            }
        }

        internal static string StripEnterpriseHostnames(string url)
        {
            Uri uri = new Uri(url, UriKind.RelativeOrAbsolute);
            if (uri.IsAbsoluteUri && !string.IsNullOrEmpty(uri.AbsolutePath) && Hostnames.ContainsKey(uri.Host))
            {
                return uri.PathAndQuery;
            }
            return url;
        }

        private static Dictionary<string, string> Hostnames
        {
            get
            {
                Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (EPiServer.Framework.Configuration.HostNameCollection hostNameCollection in EPiServerFrameworkSection.Instance.SiteHostMapping)
                {
                    foreach (EPiServer.Framework.Configuration.HostNameElement hostNameElement in hostNameCollection)
                    {
                        dictionary.Add(hostNameElement.Name, hostNameElement.Name);
                    }
                }
                return dictionary;
            }
        }
    }
}
