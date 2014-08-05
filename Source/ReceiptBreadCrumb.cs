using EasyStorage;
using Microsoft.Xna.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Xml;

namespace OuyaPurchaseHelper
{
	/// <summary>
	/// This is a little thing that writes out a file containing the players purchase status for offline play.
	/// it will contain "true" if the player has purchased the game, and "false" if they haven't
	/// If this file exists and contains "true" at the start of the game, it will assume the player has purchased it.  Trial mode will be turned off
	/// If this file does not exist or contains "false", trial mode is left on.
	/// When the Ouya receipt check gets back to the game, it will update this file.
	/// If it comes back as purchased, "true" will be written out and trial mode turned off
	/// If it comes back as not purchased, "false" will be written out and trial mode turned on.
	/// </summary>
	public class ReceiptBreadCrumb
	{
		#region Member Variables

		/// <summary>
		/// The save device.
		/// </summary>
		private IAsyncSaveDevice saveDevice;

		/// <summary>
		/// whether or not the game has been purchased
		/// </summary>
		public bool Purchased { get; set; }

		/// <summary>
		/// The location to store this high score list.
		/// This will be a relative path from the user directory, so don't put in drive letters etc.
		/// </summary>
		/// <value>The name of the folder.</value>
		private string Folder { get; set; }

		/// <summary>
		/// Flag for whether or not teh high scores have been loaded from a file
		/// </summary>
		/// <value><c>true</c> if loaded; otherwise, <c>false</c>.</value>
		private bool Loaded { get; set; }

		/// <summary>
		/// Gets or sets the name of the file.
		/// "OppositesReceipt.xml";
		/// </summary>
		/// <value>The name of the file.</value>
		private string Filename { get; set; }

		#endregion //Member Variables

		#region Methods

		/// <summary>
		/// hello standard constructor!
		/// </summary>
		public ReceiptBreadCrumb(string folder, string filename)
		{
			//set the save location
			Folder = folder;
			Filename = filename;

			Purchased = false;
			Loaded = false;
		}

		/// <summary>
		/// called once at the beginning of the program
		/// gets the storage device
		/// </summary>
		/// <param name="myGame">the current game.</param>
		public void Initialize(Game myGame)
		{
			//First add the default lists to the table
			// on Windows Phone we use a save device that uses IsolatedStorage
			// on Windows and Xbox 360, we use a save device that gets a shared StorageDevice to handle our file IO.
			#if WINDOWS_PHONE || ANDROID
			saveDevice = new IsolatedStorageSaveDevice();
			#else
			// create and add our SaveDevice
			SharedSaveDevice sharedSaveDevice = new SharedSaveDevice();
			myGame.Components.Add(sharedSaveDevice);

			// make sure we hold on to the device
			saveDevice = sharedSaveDevice;

			// hook two event handlers to force the user to choose a new device if they cancel the
			// device selector or if they disconnect the storage device after selecting it
			sharedSaveDevice.DeviceSelectorCanceled += (s, e) => e.Response = SaveDeviceEventResponse.Force;
			sharedSaveDevice.DeviceDisconnected += (s, e) => e.Response = SaveDeviceEventResponse.Force;

			// prompt for a device on the first Update we can
			sharedSaveDevice.PromptForDevice();
			#endif

			#if XBOX
			// add the GamerServicesComponent
			Components.Add(new Microsoft.Xna.Framework.GamerServices.GamerServicesComponent(this));
			#endif

			// hook an event so we can see that it does fire
			saveDevice.SaveCompleted += new SaveCompletedEventHandler(saveDevice_SaveCompleted);
		}

		/// <summary>
		/// event handler that gets fired off when a write op is completed
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="args">Arguments.</param>
		void saveDevice_SaveCompleted(object sender, FileActionCompletedEventArgs args)
		{
			//Write a message out to the deubg log so we know whats going on
			string strText = string.Format("Breadcrumb SaveCompleted, purchase history is: {0}", Purchased.ToString());
			if (null != args.Error)
			{
				strText = args.Error.Message;
			}

			Debug.WriteLine(strText);
		}

		#endregion //Methods

		#region XML Methods

		/// <summary>
		/// Save all the high scores out to disk
		/// </summary>
		public void Save()
		{
			// make sure the device is ready
			if (saveDevice.IsReady)
			{
				// save a file asynchronously. this will trigger IsBusy to return true
				// for the duration of the save process.
				saveDevice.SaveAsync(
					Folder,
					Filename,
					WriteHighScores);
			}
		}

		public void Load()
		{
			if (!Loaded)
			{
				try
				{
					//if there is a file there, load it into the system
					if (saveDevice.FileExists(Folder, Filename))
					{
						saveDevice.Load(
							Folder,
							Filename,
							ReadHighScores);
					}

					//set the Loaded flag to true since high scores only need to be laoded once
					Loaded = true;
					Debug.WriteLine(string.Format("Loaded Purchase History, purchase history is: {0}", Purchased.ToString()));
				}
				catch (Exception ex)
				{
					//now you fucked up
					Loaded = false;

					// just write some debug output for our verification
					Debug.WriteLine(ex.Message);
				}
			}
		}

		/// <summary>
		/// do the actual writing out to disk
		/// </summary>
		/// <param name="myFileStream">My file stream.</param>
		private void WriteHighScores(Stream myFileStream)
		{
			try
			{
				//open the file, create it if it doesnt exist yet
				XmlTextWriter rFile = new XmlTextWriter(myFileStream, null);
				rFile.Formatting = Formatting.Indented;
				rFile.Indentation = 1;
				rFile.IndentChar = '\t';

				//save all the high scores!
				rFile.WriteStartDocument();

				//write the high score table element
				rFile.WriteStartElement("receipt");

				//write out the name
				rFile.WriteAttributeString("Purchased", Purchased.ToString());

				rFile.WriteEndElement();

				rFile.WriteEndDocument();

				// Close the file.
				rFile.Flush();
				rFile.Close();
			}
			catch (Exception ex)
			{
				// just write some debug output for our verification
				Debug.WriteLine(ex.Message);
			}
		}

		/// <summary>
		/// to the actual reading in from disk
		/// </summary>
		/// <param name="myFileStream">My file stream.</param>
		private void ReadHighScores(Stream myFileStream)
		{
			XmlDocument xmlDoc = new XmlDocument();
			xmlDoc.Load(myFileStream);
			XmlNode rootNode = xmlDoc.DocumentElement;

			//make sure it is actually an xml node
			if (rootNode.NodeType == XmlNodeType.Element)
			{
				//make sure is correct type of node
				Debug.Assert("receipt" == rootNode.Name);

				//get the name of this dude
				XmlNamedNodeMap mapAttributes = rootNode.Attributes;
				for (int i = 0; i < mapAttributes.Count; i++)
				{
					string strName = mapAttributes.Item(i).Name;
					string strValue = mapAttributes.Item(i).Value;
					if ("Purchased" == strName)
					{
						Purchased = Convert.ToBoolean(strValue);
					}
					else
					{
						//unknwon attribute in the xml file!!!
						Debug.Assert(false);
					}
				}
			}
			else
			{
				//should be an xml node!!!
				Debug.Assert(false);
			}
		}

		#endregion //XML Methods
	}
}
