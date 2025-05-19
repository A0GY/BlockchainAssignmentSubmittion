using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace BlockchainAssignment
{
    public partial class BlockchainApp : Form
    {
       
        Blockchain blockchain;

        /* Initialize blockchain on startup of application UI */
        public BlockchainApp()
        {
            InitializeComponent();
            blockchain = new Blockchain();
            richTextBox1.Text = "blockchain initialised!";
            CLEAR.Click += new EventHandler(CLEAR_Click);
            
            // Initialize mining preference dropdown
            miningPreferenceCombo.Items.Add("Greedy (Highest Fee)");
            miningPreferenceCombo.Items.Add("Altruistic (First In, First Out)");
            miningPreferenceCombo.Items.Add("Random");
            miningPreferenceCombo.Items.Add("Self Address Preference");
            miningPreferenceCombo.SelectedIndex = 1; // Default to Altruistic
            miningPreferenceCombo.SelectedIndexChanged += MiningPreferenceCombo_SelectedIndexChanged;
            
            //  handler for changing target block time
            targetBlockTime.Text = blockchain.TargetBlockTimeSeconds.ToString();
            targetBlockTime.TextChanged += TargetBlockTime_TextChanged;
            
            // Display initial difficulty
            UpdateDifficultyLabel();
        }
        
        // Update difficulty label
        private void UpdateDifficultyLabel()
        {
            currentDifficultyLabel.Text = $"Current Difficulty: {blockchain.CurrentDifficulty:F2}";
        }
        
        // Handle target block time changes
        private void TargetBlockTime_TextChanged(object sender, EventArgs e)
        {
            try
            {
                double newTarget = Double.Parse(targetBlockTime.Text);
                if (newTarget >= 1.0) // least 1 second
                {
                    blockchain.TargetBlockTimeSeconds = newTarget;
                    UpdateText($"Target block time set to {newTarget:F1} seconds");
                }
                else
                {
                    UpdateText("Target block time must be at least 1.0 second");
                }
            }
            catch
            {
                // Invalid input 
            }
        }

        // Handle changes to the mining preference
        private void MiningPreferenceCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Update the blockchain's mining preference 
            switch (miningPreferenceCombo.SelectedIndex)
            {
                case 0:
                    blockchain.CurrentPreference = MiningPreference.Greedy;
                    break;
                case 1:
                    blockchain.CurrentPreference = MiningPreference.Altruistic;
                    break;
                case 2:
                    blockchain.CurrentPreference = MiningPreference.Random;
                    break;
                case 3:
                    blockchain.CurrentPreference = MiningPreference.SelfAddress;
                    break;
            }
            
            UpdateText($"Mining preference set to: {miningPreferenceCombo.Text}");
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        // Duplicate the value 
        private void button1_Click(object sender, EventArgs e)
        {
            if (Int32.TryParse(blockIndex.Text, out int index))
                richTextBox1.Text = blockchain.getBlockAsString(index);
            else
                richTextBox1.Text = "Please enter a valid number";

        }

        private void PK_Click(object sender, EventArgs e)
        {
            if (Wallet.Wallet.ValidatePrivateKey(Private_Key.Text, Public_Key.Text))
                richTextBox1.Text = "Private Key is valid";
            else
                richTextBox1.Text = "Private Key is invalid";


        }



        private void GenW_Click(object sender, EventArgs e) // generate the wallet 
        {
            String privateKey;
            Wallet.Wallet wallet = new Wallet.Wallet(out privateKey);
            Public_Key.Text = wallet.publicID;
            Private_Key.Text = privateKey;
        }












        // clear the screen
        private void CLEAR_Click(object sender, EventArgs e)
        {
            richTextBox1.Text = String.Empty;
        }

        private void button2_Click(object sender, EventArgs e) // validation of the private key 
        {
            richTextBox1.Text = Wallet.Wallet.ValidatePrivateKey(Private_Key.Text, Public_Key.Text)
                ? "Private Key is valid"
                : "Private Key is invalid";
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        
        // Create a new pending transaction and add it to the transaction pool
        private void CreateTransaction_Click(object sender, EventArgs e)
        {
            try
            {
                //  Check if sender has sufficient funds before creating transaction
                double bal = blockchain.GetBalance(Public_Key.Text);
                double amount_to_send = Double.Parse(amount.Text);
                double fee_to_pay = Double.Parse(fee.Text);
                
                if (bal < amount_to_send + fee_to_pay)
                {
                    UpdateText("Insufficient funds for transaction. Current balance: " + bal);
                    return;
                }
                
                Transaction transaction = new Transaction(
                    Public_Key.Text,
                    reciever.Text,
                    amount_to_send,
                    fee_to_pay,
                    Private_Key.Text
                );
                blockchain.transactionPool.Add(transaction);
                UpdateText(transaction.ToString() + "\n\nTransaction added to pool. Current pool size: " + blockchain.transactionPool.Count);
            }
            catch (Exception ex)
            {
                UpdateText($"Error creating transaction: {ex.Message}");
            }
        }

        // Helper method to update the UI with a provided message
        private void UpdateText(String text)
        {
            
            richTextBox1.Text = text;
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void label3_Click(object sender, EventArgs e)
        {

        }
        
        // Display current difficulty settings
        private void ShowDifficulty_Click(object sender, EventArgs e)
        {
            UpdateText(blockchain.GetDifficultyInfo());
        }

        private void newBlock_Click(object sender, EventArgs e)
        {
            try
            {
                // Calculate new difficulty based on previous blocks
                double newDifficulty = blockchain.CalculateNewDifficulty();
                
                // Update difficulty display
                UpdateDifficultyLabel();
            
                // Show current mining preference
                string preferenceText = $"Mining with preference: {miningPreferenceCombo.Text}";
                string difficultyText = $"Mining with dynamic difficulty: {newDifficulty:F2}";
                UpdateText(preferenceText + "\n" + difficultyText);
                
                // Get pending transactions with detailed information
                var txResult = blockchain.GetPendingTransactionsWithDetails(Public_Key.Text);
                List<Transaction> txs = txResult.Item1;
                string txDetails = txResult.Item2;
                
                // Mine the block with selected transactions and dynamic difficulty
                Block newBlock = new Block(blockchain.GetLastBlock(), txs, Public_Key.Text, newDifficulty);
                
                // Add to blockchain
                blockchain.Blocks.Add(newBlock);
                
                // Show detailed information about the mining process
                UpdateText(
                    preferenceText + "\n" + difficultyText + "\n\n" + 
                    txDetails + "\n\n" + 
                    $"New block successfully mined in {newBlock.miningTime.TotalSeconds:F2} seconds\n" +
                    $"Target time was {blockchain.TargetBlockTimeSeconds:F2} seconds\n" +
                    "Block index: " + (blockchain.Blocks.Count - 1) + "\n" +
                    "Total blocks: " + blockchain.Blocks.Count + "\n\n" +
                    newBlock.ToString()
                );
                
                // Update difficulty label
                UpdateDifficultyLabel();
            }
            catch (Exception ex)
            {
                UpdateText($"Error mining block: {ex.Message}");
            }
        }

        
        // Part 5Validate the integrity of the state of the Blockchain
        private void Validate_Click(object sender, EventArgs e)
        {
            // Genesis Block  Check only hash as no transactions are currently present
            if(blockchain.Blocks.Count == 1)
            {
                if (!Blockchain.ValidateHash(blockchain.Blocks[0])) 	// Recompute Hash to check validity
                    UpdateText("Blockchain is invalid");
                else
                    UpdateText("Blockchain is valid");
                return;
            }

            for (int i=1; i<blockchain.Blocks.Count; i++)
            {
                if(
                    blockchain.Blocks[i].prevHash != blockchain.Blocks[i - 1].hash || 	// Check hash chain
                    !Blockchain.ValidateHash(blockchain.Blocks[i]) ||  		// Check each blocks hash
                    !Blockchain.ValidateMerkleRoot(blockchain.Blocks[i]) 		// Check transaction integrity using Merkle Root
                )
                {
                    UpdateText("Blockchain is invalid");
                    return;
                }
            }
            UpdateText("Blockchain is valid\n\n" + blockchain.GetPreferenceStats());
        }

        
        // Check the balance of current user
        private void CheckBalance_Click(object sender, EventArgs e)
        {
            UpdateText(blockchain.GetBalance(Public_Key.Text).ToString() + " Assignment Coin");
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            //validate chain
        }

        private void button3_Click(object sender, EventArgs e)
        {
            // check balance
        }

        private void currentDifficultyLabel_Click(object sender, EventArgs e)
        {

        }
    }
}











