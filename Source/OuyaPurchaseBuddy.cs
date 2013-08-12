using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic; 
using Ouya.Console.Api;
using Ouya.Csharp;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Xna.Framework.GamerServices;

namespace OuyaPurchaseHelper
{
	/// <summary>
	/// This is a thing that does some of the work for the OUYA purchasing api
	/// </summary>
	public class OuyaPurchaseBuddy
	{
		#region Member Variables

		/// <summary>
		/// List of all the products available to this dude
		/// </summary>
		private Task<IList<Product>> TaskRequestProducts = null;

		/// <summary>
		/// whether or not this dude has bought the game
		/// </summary>
		private Task<bool> TaskRequestPurchase = null;

		/// <summary>
		/// The task to get the gamer id
		/// </summary>
		private Task<string> TaskRequestGamer = null;

		/// <summary>
		/// The task to get a list of the dude's receipts
		/// </summary>
		private Task<IList<Receipt>> TaskRequestReceipts = null;

		/// <summary>
		/// For purchases all transactions need a unique id
		/// </summary>
		private string m_uniquePurchaseId = string.Empty;

		/// <summary>
		/// Whether or not we already checked all the receipts
		/// </summary>
		public bool ReceiptsChecked { get; private set; }

		/// <summary>
		/// Whether or not the gamer checked.
		/// </summary>
		private bool GamerChecked = false;

		/// <summary>
		/// Whether or not the purchasables checked.
		/// </summary>
		private bool PurchasablesChecked = false;

		/// <summary>
		/// The name of the "full game" item the player can buy.
		/// </summary>
		protected string PurchaseItem { get; set; }

		#endregion //Member Variables

		#region Properties

		/// <summary>
		/// The purchase facade.
		/// </summary>
		public OuyaFacade PurchaseFacade { get; set; }

		#endregion //Properties

		#region Initialization

		/// <summary>
		/// Initializes a new instance of the <see cref="OuyaPurchaseHelper.OuyaPurchaseBuddy"/> class.
		/// </summary>
		/// <param name="game">Game.</param>
		/// <param name="purchasables">Purchasables.</param>
		/// <param name="purchaseFacade">Purchase facade.</param>
		/// <param name="fullGame">This is the name of the item the player purchases to unlock the full version of the game.
		/// This has to match the Identifier of an item on your https://devs.ouya.tv/developers/products page!!!</param>
		public OuyaPurchaseBuddy(Game game, IList<Purchasable> purchasables, OuyaFacade purchaseFacade, string fullGame)
		{
			ReceiptsChecked = false;
			PurchaseItem = fullGame;

			//always start in trial mode
			SetTrialMode(true);

			//Get the list of purchasable items
			this.PurchaseFacade = purchaseFacade;
			TaskRequestProducts = PurchaseFacade.RequestProductListAsync(purchasables);
		}

		/// <summary>
		/// clear this thing out?
		/// </summary>
		void ClearPurchaseId()
		{
			m_uniquePurchaseId = string.Empty;
		}

		/// <summary>
		/// Requests the receipts.
		/// Called once at the beginning of the game.
		/// </summary>
		public void RequestReceipts()
		{
			//get the player uuid
			Debug.WriteLine("Requesting gamer uuid...");
			TaskRequestGamer = PurchaseFacade.RequestGamerUuidAsync();

			//get the receipts
			Debug.WriteLine("Requesting receipts...");
			TaskRequestReceipts = PurchaseFacade.RequestReceiptsAsync();
		}

		#endregion //Initialization

		#region Update and Draw

		/// <summary>
		/// This is a polling method that needs to be called once every update
		/// Checks the status of all our Ouya tasks.
		/// </summary>
		public void Update()
		{
			//If it is't trial mode, don't do any of this other stuff
			if (ReceiptsChecked && !GetTrialMode())
			{
				return;
			}

			//Check on that request products task...
			if ((null != TaskRequestProducts) && !PurchasablesChecked)
			{
				AggregateException exception = TaskRequestProducts.Exception;
				if (null != exception)
				{
					Debug.WriteLine(string.Format("Request Products has exception. {0}", exception));
					TaskRequestProducts.Dispose();
					TaskRequestProducts = null;
				}
				else
				{
					if (TaskRequestProducts.IsCanceled)
					{
						Debug.WriteLine("Request Products has cancelled.");
						PurchasablesChecked = true;
					}
					else if (TaskRequestProducts.IsCompleted)
					{
						Debug.WriteLine("Request Products has completed with results.");
						if (null != TaskRequestProducts.Result)
						{
							//check the last item in the list
							CheckReceipt();
						}
						PurchasablesChecked = true;
					}
				}
			}

			//Check on that purchase task...
			if (null != TaskRequestPurchase)
			{
				AggregateException exception = TaskRequestPurchase.Exception;
				if (null != exception)
				{
					Debug.WriteLine(string.Format("Request Purchase has exception. {0}", exception));
					TaskRequestPurchase.Dispose();
					TaskRequestPurchase = null;
					ClearPurchaseId();
				}
				else
				{
					if (TaskRequestPurchase.IsCanceled)
					{
						Debug.WriteLine("Request Purchase has cancelled.");
						TaskRequestPurchase = null;
						ClearPurchaseId(); //clear the purchase id
					}
					else if (TaskRequestPurchase.IsCompleted)
					{
						if (TaskRequestPurchase.Result)
						{
							//this means they were able to buy it
							Debug.WriteLine("Request Purchase has completed succesfully.");
							SetTrialMode(false);
						}
						else
						{
							Debug.WriteLine("Request Purchase has completed with failure.");
						}
						TaskRequestPurchase = null;
						ClearPurchaseId(); //clear the purchase id
					}
				}
			}

			//Check on our receipt task...
			if ((null != TaskRequestReceipts) && !ReceiptsChecked)
			{
				//Did it blow up?  Clear it out to prevent killing the app.
				AggregateException exception = TaskRequestReceipts.Exception;
				if (null != exception)
				{
					Debug.WriteLine(string.Format("Request Receipts has exception. {0}", exception));
					TaskRequestReceipts.Dispose();
					TaskRequestReceipts = null;
				}
				else
				{
					//If it is still trial mode, check if that thing has completed.
					if (TaskRequestReceipts.IsCanceled)
					{
						Debug.WriteLine("Request Receipts has cancelled.");
						ReceiptsChecked = true;
					}
					else if (TaskRequestReceipts.IsCompleted)
					{
						//Ok, the receipts task has come back with an answer.
						Debug.WriteLine("Request Receipts has completed.");
						if (null != TaskRequestReceipts.Result)
						{
							//check the last item in the list
							CheckReceipt();
						}
						ReceiptsChecked = true;
					}
				}
			}

			// touch exception property to avoid killing app
			if ((null != TaskRequestGamer) && !GamerChecked)
			{
				AggregateException exception = TaskRequestGamer.Exception;
				if (null != exception)
				{
					Debug.WriteLine(string.Format("Request Gamer UUID has exception. {0}", exception));
					TaskRequestGamer.Dispose();
					TaskRequestGamer = null;
				}
				else
				{
					//If it is still trial mode, check if that thing has completed.
					if (TaskRequestGamer.IsCanceled)
					{
						Debug.WriteLine("Request Gamer UUID has cancelled.");
						GamerChecked = true;
					}
					else if (TaskRequestGamer.IsCompleted)
					{
						//ok, the gamer task cam back with an answer...
						Debug.WriteLine("Request Gamer UUID has completed.");

						GamerChecked = true;
					}
				}
			}
		}
		
		#endregion //Update and Draw

		#region Public Methods
			
		/// <summary>
		/// Got a message back from Ouya... check the receipt, has the player bought the game already
		/// </summary>
		/// <param name="receiptIndex">Receipt index.</param>
		protected virtual void CheckReceipt()
		{
			//If we've already done this check, don't keep doing it
			if (ReceiptsChecked)
			{
				return;
			}

			Debug.WriteLine("Checking receipts...");

			//Get the text from the receipt
			if ((null != TaskRequestReceipts) &&
			    (null == TaskRequestReceipts.Exception) &&
			    !TaskRequestReceipts.IsCanceled &&
			    TaskRequestReceipts.IsCompleted)
			{
				Debug.WriteLine("Found receipts...");
				if  (null != TaskRequestReceipts.Result)
				{
					bool bFound = false;
					foreach (Receipt receipt in TaskRequestReceipts.Result)
					{
						Debug.WriteLine(string.Format("The receipt item is {0}", receipt.Identifier));
						if (PurchaseItem == receipt.Identifier)
						{
							bFound = true;
							break;
						}
					}

					if (bFound)
					{
						//ok, we got the purchasable item and the receipt for it, so trial mode is OVER
						Debug.WriteLine("Trial mode is over!");
						SetTrialMode(false);
					}
					else
					{
						Debug.WriteLine("Checked receipts, and player has not purchased.");
						SetTrialMode(true);
					}
				}
				else if (null != TaskRequestReceipts.Result)
				{
					Debug.WriteLine(string.Format("Found receipts {0}.", TaskRequestReceipts.Result.Count));
				}
				else
				{
					Debug.WriteLine("No result on the receipts list?");
				}
			}
		}

		/// <summary>
		/// User selected an item to try and buy the full game
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		public virtual void PurchaseFullVersion()
		{
			if (GetTrialMode())
			{
				if (null != TaskRequestProducts &&
				    null == TaskRequestProducts.Exception &&
				    !TaskRequestProducts.IsCanceled &&
				    TaskRequestProducts.IsCompleted)
				{
					Product product = TaskRequestProducts.Result[0];
					if (string.IsNullOrEmpty(m_uniquePurchaseId))
					{
						m_uniquePurchaseId = Guid.NewGuid().ToString().ToLower();
					}
					TaskRequestPurchase = PurchaseFacade.RequestPurchaseAsync(product, m_uniquePurchaseId);
				}
			}
		}

		/// <summary>
		/// Sets the trial mode flag.
		/// this method gets called when:
		/// 	the player purchases the game
		/// 	we have verified that they already purchased 
		/// 	the ouya service gets back to us that they have not purchased
		/// </summary>
		/// <param name="IsTrialMode">If set to <c>true</c> is trial mode.</param>
		public virtual void SetTrialMode(bool bIsTrialMode)
		{
			Guide.IsTrialMode = bIsTrialMode;
		}

		/// <summary>
		/// Gets the trial mode.
		/// </summary>
		/// <returns><c>true</c>, if trial mode was gotten, <c>false</c> otherwise.</returns>
		public virtual bool GetTrialMode()
		{
			return Guide.IsTrialMode;
		}

		#endregion //Public Methods
	}
}
