using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockchainAssignment
{
    // Mining preference options for transaction selection
    public enum MiningPreference
    {
        Greedy,      // Highest fee first
        Altruistic,  // Longest wait first (first in, first out)
        Random,      // Random selection
        SelfAddress  // Prioritize transactions involving miner's address
    }

    class Blockchain
    {
       
        public List<Block> Blocks; // list of block in chain

        private int transactionsPerBlock = 5;
        public List<Transaction> transactionPool = new List<Transaction>();  // Pending transactions waiting to be mined
        
        //  stores current mining preference
        public MiningPreference CurrentPreference { get; set; } = MiningPreference.Altruistic;
        
        // Track number of transactions processed by each strategy
        public int GreedyTxCount { get; private set; } = 0;
        public int AltruisticTxCount { get; private set; } = 0;
        public int RandomTxCount { get; private set; } = 0;
        public int SelfAddressTxCount { get; private set; } = 0;
        
        // Dynamic difficulty settings
        public double TargetBlockTimeSeconds { get; set; } = 10.0;  // Target time in seconds between blocks
        public double CurrentDifficulty { get; private set; } = 3.0; // Starting difficulty
        
        // For adjustment smoothing (avoid wild swings)
        private int difficultyAdjustmentBlocks = 2;   // How many blocks to average for adjustment
        private double maxAdjustmentFactor = 0.4;    // Maximum adjustment per block (40%)
        
        /* Blockchain Constructor  Initialises a new blockchain with a single genesis block */
        public Blockchain()
        {
           this.Blocks = new List<Block>() {
                new Block()
            };
        }

        /* Helper function to get a block at a user specified index */
        public String getBlockAsString(int index)
        {
            if (index >= 0 && index < Blocks.Count)
                return Blocks[index].ToString();
            return "Block does not exist at index: " + index.ToString();
        }

        /* Task 2 Calculate new difficulty based on recent mining times */
        public double CalculateNewDifficulty()
        {
            // For genesis block or first few blocks, use default difficulty
            if (Blocks.Count <= 1)
            {
                return CurrentDifficulty;
            }
            
            // Get the number of blocks to analyze 
            int blocksToAnalyze = Math.Min(difficultyAdjustmentBlocks, Blocks.Count - 1);
            
            // Calculate average mining time from recent blocks
            double totalMiningTime = 0;
            for (int i = Blocks.Count - 1; i >= Blocks.Count - blocksToAnalyze; i--)
            {
                totalMiningTime += Blocks[i].miningTime.TotalSeconds;
            }
            double averageMiningTime = totalMiningTime / blocksToAnalyze;
            
            // Determine adjustment factor based on comparison with target time
            double timeRatio = averageMiningTime / TargetBlockTimeSeconds;
            
            // If mining is too slow, decrease difficultyif too fast, increase it
            // Limit adjustment to avoid wild swings
            double adjustmentFactor = Math.Max(
                Math.Min(timeRatio, 1 + maxAdjustmentFactor),
                1 - maxAdjustmentFactor
            );
            
            // Inverse relationship - higher time ratio means lower difficulty
            double newDifficulty = CurrentDifficulty / adjustmentFactor;
            
            // Ensure minimum difficulty (can't go below 1.0)
            newDifficulty = Math.Max(1.0, newDifficulty);
            
            // Update current difficulty
            CurrentDifficulty = newDifficulty;
            
            return newDifficulty;
        }
        
        /* Get info about current difficulty settings */
        public string GetDifficultyInfo()
        {
            StringBuilder info = new StringBuilder();
            info.AppendLine("=== DIFFICULTY SETTINGS ===");
            info.AppendLine($"Target Block Time: {TargetBlockTimeSeconds:F1} seconds");
            info.AppendLine($"Current Difficulty: {CurrentDifficulty:F2}");
            
            if (Blocks.Count > 1)
            {
                // Calculate average actual mining time
                int blocksToAnalyze = Math.Min(5, Blocks.Count - 1);
                double totalMiningTime = 0;
                
                for (int i = Blocks.Count - 1; i >= Blocks.Count - blocksToAnalyze; i--)
                {
                    totalMiningTime += Blocks[i].miningTime.TotalSeconds;
                }
                
                double avgTime = totalMiningTime / blocksToAnalyze;
                info.AppendLine($"Average Mining Time (last {blocksToAnalyze} blocks): {avgTime:F2} seconds");
                
                if (avgTime < TargetBlockTimeSeconds * 0.8)
                    info.AppendLine("Status: Mining too fast, difficulty will increase");
                else if (avgTime > TargetBlockTimeSeconds * 1.2)
                    info.AppendLine("Status: Mining too slow, difficulty will decrease");
                else
                    info.AppendLine("Status: Mining at appropriate speed");
            }
            
            return info.ToString();
        }

        
        // Returns selected transactions and a description of the selection process
        public Tuple<List<Transaction>, string> GetPendingTransactionsWithDetails(string minerAddress = "")
        {
            // No transactions - just return empty list
            if (transactionPool.Count == 0)
                return new Tuple<List<Transaction>, string>(new List<Transaction>(), "No pending transactions available");

            // Make a copy of the pool that we can sort without affecting original order
            List<Transaction> sortedPool = new List<Transaction>(transactionPool);
            string selectionDetails = "";
            
            // Sort transactions based on preference
            switch (CurrentPreference)
            {
                case MiningPreference.Greedy:
                    // Sort by highest fee first
                    sortedPool = sortedPool.OrderByDescending(tx => tx.fee).ToList();
                    selectionDetails = "Greedy selection: Transactions ordered by highest fee first";
                    GreedyTxCount += Math.Min(transactionsPerBlock, sortedPool.Count);
                    break;
                    
                case MiningPreference.Altruistic:
                    // Sort by timestamp (oldest first) 
                    sortedPool = sortedPool.OrderBy(tx => tx.timestamp).ToList();
                    selectionDetails = "Altruistic selection: Transactions ordered by timestamp (oldest first)";
                    AltruisticTxCount += Math.Min(transactionsPerBlock, sortedPool.Count);
                    break;
                    
                case MiningPreference.Random:
                    // Shuffle the transactions randomly
                    Random rnd = new Random();
                    sortedPool = sortedPool.OrderBy(tx => rnd.Next()).ToList();
                    selectionDetails = "Random selection: Transactions selected in random order";
                    RandomTxCount += Math.Min(transactionsPerBlock, sortedPool.Count);
                    break;
                    
                case MiningPreference.SelfAddress:
                    if (!string.IsNullOrEmpty(minerAddress))
                    {
                        // Check if any transactions involve miners address
                        bool hasRelatedTx = sortedPool.Any(tx => 
                            tx.senderAddress == minerAddress || 
                            tx.recipientAddress == minerAddress);
                        
                        if (hasRelatedTx)
                        {
                            // Prioritize transactions involving mines address
                            sortedPool = sortedPool
                                .OrderByDescending(tx => 
                                    tx.senderAddress == minerAddress || 
                                    tx.recipientAddress == minerAddress ? 1 : 0)
                                .ThenByDescending(tx => tx.fee) // Secondary sort by fee
                                .ToList();
                            selectionDetails = "Self Address preference: Prioritizing transactions involving miner's address";
                        }
                        else
                        {
                            // Fall back to greedy if no transactions involve miner
                            sortedPool = sortedPool.OrderByDescending(tx => tx.fee).ToList();
                            selectionDetails = "Self Address preference: No transactions involving miner's address found, falling back to fee-based selection";
                        }
                    }
                    else
                    {
                        // Fall back to greedy if no miner address provided
                        sortedPool = sortedPool.OrderByDescending(tx => tx.fee).ToList();
                        selectionDetails = "Self Address preference: No miner address provided, falling back to fee-based selection";
                    }
                    SelfAddressTxCount += Math.Min(transactionsPerBlock, sortedPool.Count);
                    break;
            }
            
            //only take what we need instead of sorting entire pool
            int n = Math.Min(transactionsPerBlock, sortedPool.Count);
            List<Transaction> selected = sortedPool.Take(n).ToList();
            
            // More detailed selection report
            selectionDetails += $"\nSelected {selected.Count} out of {transactionPool.Count} pending transactions";
            
            // Add fee details if any transactions were selected
            if (selected.Count > 0)
            {
                double totalFees = selected.Sum(tx => tx.fee);
                double avgFee = totalFees / selected.Count;
                selectionDetails += $"\nTotal fees: {totalFees:F2}, Average fee: {avgFee:F2}";
            }
            
            // Remove selected transactions from the pool
            foreach (var tx in selected)
            {
                transactionPool.Remove(tx);
            }
            
            return new Tuple<List<Transaction>, string>(selected, selectionDetails);
        }
        
        // Maintain backward compatibility with the original method
        public List<Transaction> GetPendingTransactions(string minerAddress = "")
        {
            return GetPendingTransactionsWithDetails(minerAddress).Item1;
        }

        //   Helper to grab the most recently added block in the chain
        public Block GetLastBlock()
        {
            return Blocks[Blocks.Count - 1];
        }

        //  Check validity of a block's hash by recomputing the hash and comparing with the mined value
        public static bool ValidateHash(Block block)
        {
            String rehash = block.CreateHash();
            return rehash.Equals(block.hash);
        }

        //  Check validity of the merkle root by recalculating the root and comparing with the mined value
        public static bool ValidateMerkleRoot(Block block)
        {
            String reMerkle = Block.MerkleRoot(block.transactionList);
            return reMerkle.Equals(block.merkleRoot);
        }

        //   Check the balance associated with a wallet based on the public key
        public double GetBalance(String address)
        {
            double balance = 0;                   // Accumulator value for current Wallet

            foreach(Block block in Blocks)         // Loop through all approved transactions in order to assess account balance
            {
                foreach(Transaction transaction in block.transactionList)
                {
                    if (transaction.recipientAddress.Equals(address))
                    {
                        balance += transaction.amount;           // Credit funds received
                    }
                    if (transaction.senderAddress.Equals(address))
                    {
                        balance -= (transaction.amount + transaction.fee);   // Debit payments placed
                    }
                }
            }
            return balance;
        }
        
        // Get statistics about transaction selections
        public string GetPreferenceStats()
        {
            return $"Transaction Selection Statistics:\n" +
                   $"- Greedy: {GreedyTxCount} transactions\n" +
                   $"- Altruistic: {AltruisticTxCount} transactions\n" +
                   $"- Random: {RandomTxCount} transactions\n" +
                   $"- Self-Address: {SelfAddressTxCount} transactions";
        }

        public override string ToString()
        {
            return String.Join("\n\n", Blocks);
        }
    }
}
