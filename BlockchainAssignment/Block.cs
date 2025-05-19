using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics; // For Stopwatch
using System.Collections.Concurrent; // For thread-safe collections

namespace BlockchainAssignment
{
    
    class Block
    {
        // --- Block Metadata ---
        public DateTime timestamp;             // Time when this block was created - made public for dynamic difficulty
        private int index;                      // Position of the block in the chain
        public double difficulty;              // Modified for Task 2 - now a double for finer adjustments
                    
        // --- PoW & Chaining ---
        public long nonce;                      // Nonce value adjusted during mining
        public String prevHash,                 // Hash of the previous block
                      hash;                     // Hash of this block (meets difficulty)

        // --- Transaction Data ---
        public List<Transaction> transactionList; // Transactions included in this block
        public String merkleRoot;               // Merkle root of all transaction hashes

        // --- Reward Data ---
        public String minerAddress;             // Wallet address to receive mining reward
        public double reward;                   // Fixed reward + accumulated fees

        // Mining time info
        public TimeSpan miningTime;             // Added for Task 2 - time taken to mine the block

        // Parallel Mining Performance Data
        public int ThreadCount { get; private set; } // Number of threads used for mining
        public long HashesComputed { get; private set; } // Total number of hashes computed
        public double HashRate { get; private set; } // Hashes per second

        // Default max threads to use (0 = all available)
        private static int maxThreads = 0;

        
         // Genesis Block Constructor
         // Creates the very first block with no transactions.
         
         
        public Block()
        {
            this.timestamp = DateTime.Now;      // creation time
            this.index = 0;                     // genesis index
            this.difficulty = 3.0;              // Initial difficulty - modified for Task 2
            this.transactionList = new List<Transaction>();// no txs yet
            this.prevHash = String.Empty;       // no parent block
            this.hash = Mine();                 // perform PoW for genesis
        }

        
        public Block(Block lastBlock, List<Transaction> transactions, String minerAddress, double newDifficulty)
        {
            // Set initial timestamp for mining time calculation
            DateTime startTime = DateTime.Now;
            
            this.timestamp = startTime;
            this.index = lastBlock.index + 1;    // next position
            this.prevHash = lastBlock.hash;      // link to parent
            this.minerAddress = minerAddress;    // who gets the coinbase
            this.reward = 1.0;                   // fixed block reward
            this.difficulty = newDifficulty;     // Dynamic difficulty 

            // 1) create and add the reward transaction 
            transactions.Add(createRewardTransaction(transactions));

            // 2) set our transactions and compute the Merkle root
            this.transactionList = new List<Transaction>(transactions);
            this.merkleRoot = MerkleRoot(transactionList);

            // 3) perform proof of work to find a valid hash
            this.hash = Mine();
            
            // Calculate & store mining time
            this.miningTime = DateTime.Now - startTime;
        }

        
         // Generate the SHA-256 hash of this block,
         
        public String CreateHash(long nonceValue)
        {
            SHA256 hasher = SHA256Managed.Create();
            // Concatenate all fields that define this block
            String input = timestamp.ToString()
                         + index
                         + prevHash
                         + nonceValue       // Use provided nonce instead of  variable
                         + merkleRoot
                         + difficulty.ToString("F1");
            Byte[] hashBytes = hasher.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Convert to hexadecimal string
            StringBuilder sb = new StringBuilder();
            foreach (byte b in hashBytes)
                sb.AppendFormat("{0:x2}", b);
            return sb.ToString();
        }

        // Wrapper for backward compatibility
        public String CreateHash()
        {
            return CreateHash(nonce);
        }

        
         //Check if a hash meets the difficulty requirement
         
        private bool IsValidHash(string hash)
        {
            // Get whole part of difficulty (full zeros)
            int wholeDigits = (int)Math.Floor(difficulty);
            String fullZeros = new string('0', wholeDigits);
            
            // Get fractional part to determine partial matching
            double fraction = difficulty - wholeDigits;
            int partialConstraint = (int)(16 * fraction);
            
            // Check if hash starts with full zeros
            if (hash.StartsWith(fullZeros))
            {
                if (wholeDigits == difficulty || partialConstraint == 0)
                {
                    // Exact match for whole number difficulty
                    return true;
                }
                
                // For fractional difficulty, check next digit
                char nextChar = hash[wholeDigits];
                int value = Convert.ToInt32(nextChar.ToString(), 16);
                
                // If next digit is less than our constraint, hash is valid
                if (value < partialConstraint)
                {
                    return true;
                }
            }
            return false;
        }

        private int GetThreadCount()
        {
            if (maxThreads > 0)
                return maxThreads;
                
            // Use processor count but leave one core free for UI/system
            int processorCount = Environment.ProcessorCount;
            return Math.Max(1, processorCount - 1);
        }

        
         // Single-threaded implementation
        
         
        public String MineSingleThreaded()
        {
            // For timing
            Stopwatch sw = new Stopwatch();
            sw.Start();
            
            nonce = 0;
            long hashesComputed = 0;
            String hash = CreateHash(nonce);
            
            // Keep mining until we find a valid hash
            while (!IsValidHash(hash))
            {
                nonce++;
                hashesComputed++;
                hash = CreateHash(nonce);
            }
            
            sw.Stop();
            
            // Calculate and store performance metrics
            ThreadCount = 1;
            HashesComputed = hashesComputed;
            HashRate = (double)hashesComputed / sw.Elapsed.TotalSeconds;
            
            return hash;
        }

        
         // Multi-threaded implementation
         
         
        public String Mine()
        {
            // Main stopwatch for overall performance
            Stopwatch sw = new Stopwatch();
            sw.Start();
            
            // Thread coordination
            int threadCount = GetThreadCount();
            bool foundValidHash = false;
            string validHash = "";
            long validNonce = 0;
            
            // For thread-safe counting of total hashes
            long totalHashes = 0;
            
            // Use CancellationToken to stop all threads when solution is found
            using (var cts = new CancellationTokenSource())
            {
                // Thread-local variables that will be collected at the end
                ConcurrentBag<long> threadHashCounts = new ConcurrentBag<long>();
                
                // Create and start worker threads
                var tasks = new List<Task>();
                
                for (int i = 0; i < threadCount; i++)
                {
                    // Each thread gets its own starting nonce range
                    long startNonce = i * 1000000L;
                    
                    tasks.Add(Task.Run(() => 
                    {
                        // Thread-local variables
                        long currentNonce = startNonce;
                        long hashesTried = 0;
                        Random rnd = new Random(Thread.CurrentThread.ManagedThreadId); // For more randomized nonce ranges
                        
                        // Try to find a valid hash until solution is found or cancelled
                        while (!foundValidHash && !cts.Token.IsCancellationRequested)
                        {
                            string hash = CreateHash(currentNonce);
                            hashesTried++;
                            
                            // Check if this is a valid hash
                            if (IsValidHash(hash))
                            {
                                // Found a solution! 
                                lock (this) // Ensure only one thread updates the result
                                {
                                    if (!foundValidHash) // Double-check in case another thread found a solution
                                    {
                                        foundValidHash = true;
                                        validHash = hash;
                                        validNonce = currentNonce;
                                        
                                        // Tell other threads to stop
                                        cts.Cancel();
                                    }
                                }
                                break;
                            }
                            
                            // Try next nonce  add some randomness to avoid duplicating work
                            currentNonce += rnd.Next(1, threadCount * 2);
                        }
                        
                        // Record how many hashes this thread computed
                        threadHashCounts.Add(hashesTried);
                    }, cts.Token));
                }
                
                try 
                {
                    // Wait for a solution to be found or all tasks to complete
                    Task.WaitAll(tasks.ToArray());
                }
                catch (AggregateException ae)
                {
                    // Expected if tasks are canceled - ignore
                    foreach (var e in ae.InnerExceptions)
                    {
                        if (!(e is TaskCanceledException))
                            throw; // Something unexpected happened
                    }
                }
                
                // Sum up all hashes tried by all threads
                totalHashes = threadHashCounts.Sum();
            }
            
            sw.Stop();
            
            // Update block with the solution
            this.nonce = validNonce;
            
            // Store mining statistics
            ThreadCount = threadCount;
            HashesComputed = totalHashes;
            HashRate = (double)totalHashes / sw.Elapsed.TotalSeconds;
            
            return validHash;
        }

        // Static method to set max threads to use (0 = auto)
        public static void SetMaxThreads(int threads)
        {
            maxThreads = Math.Max(0, threads);
        }

        
         // Compute the Merkle root of a list of transactions.
         
        
        public static String MerkleRoot(List<Transaction> transactionList)
        {
            List<String> hashes = transactionList.Select(t => t.hash).ToList();
            if (hashes.Count == 0) return String.Empty;
            if (hashes.Count == 1) return HashCode.HashTools.CombineHash(hashes[0], hashes[0]);

            // Pairwise combine
            while (hashes.Count > 1)
            {
                var nextLevel = new List<String>();
                for (int i = 0; i < hashes.Count; i += 2)
                {
                    if (i == hashes.Count - 1)
                        nextLevel.Add(HashCode.HashTools.CombineHash(hashes[i], hashes[i]));
                    else
                        nextLevel.Add(HashCode.HashTools.CombineHash(hashes[i], hashes[i + 1]));
                }
                hashes = nextLevel;
            }
            return hashes[0];
        }

        
        public Transaction createRewardTransaction(List<Transaction> transactions)
        {
            double totalFees = transactions.Aggregate(0.0, (sum, tx) => sum + tx.fee);
            return new Transaction(
                "Mine Rewards",         // reserved sender address
                minerAddress,           // who receives the coins
                reward + totalFees,     // reward + all fees
                0,                      // no fee on reward tx
                ""                      // no private key needed
            );
        }

        
          //Nice, human-readable representation for UI output,
         
         
        public override string ToString()
        {
            return "[BLOCK START]"
                + "\nIndex: " + index
                + "\tTimestamp: " + timestamp
                + "\nPrevious Hash: " + prevHash
                + "\n-- PoW --"
                + "\nDifficulty Level: " + difficulty.ToString("F2")
                + "\nMining Time: " + miningTime.TotalSeconds.ToString("F2") + " seconds"
                + "\nThreads Used: " + ThreadCount
                + "\nHashes Computed: " + HashesComputed.ToString("N0") 
                + "\nHash Rate: " + HashRate.ToString("N0") + " hashes/sec"
                + "\nNonce: " + nonce
                + "\nHash: " + hash
                + "\n-- Rewards --"
                + "\nReward: " + reward
                + "\nMiner Address: " + minerAddress
                + "\n-- " + transactionList.Count + " Transactions --"
                + "\nMerkle Root: " + merkleRoot
                + "\n" + String.Join("\n", transactionList)
                + "\n[BLOCK END]";
        }
    }
}
