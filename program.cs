#nullable disable // Null uyarılarını kapatır (Sarı uyarıları görmezsin)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace IsletimSistemleriOdev
{
    // --- SÜREÇ (PROCESS) SINIFI ---
    public class Process
    {
        public string ID { get; set; }
        public double ArrivalTime { get; set; }
        public double BurstTime { get; set; }
        public int Priority { get; set; }

        public double RemainingTime { get; set; }
        public double CompletionTime { get; set; }
        public double TurnaroundTime { get; set; }
        public double WaitingTime { get; set; }
        public double StartTime { get; set; } = -1;

        public Process Clone()
        {
            return (Process)this.MemberwiseClone();
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // CSV Dosya Adı
            string dosyaYolu = "veri.csv"; 
            
            List<Process> hamListe = new List<Process>();

            // Dosya Okuma
            if (File.Exists(dosyaYolu))
            {
                hamListe = CSVOku(dosyaYolu);
                Console.WriteLine($"Dosya okundu. Süreç sayisi: {hamListe.Count}");
            }
            else
            {
                Console.WriteLine("CSV bulunamadi, TEST verisi kullaniliyor.");
                hamListe.Add(new Process { ID = "P1", ArrivalTime = 0, BurstTime = 10, Priority = 3, RemainingTime = 10 });
                hamListe.Add(new Process { ID = "P2", ArrivalTime = 2, BurstTime = 5, Priority = 1, RemainingTime = 5 });
                hamListe.Add(new Process { ID = "P3", ArrivalTime = 5, BurstTime = 2, Priority = 2, RemainingTime = 2 });
            }

            // --- TÜM ALGORİTMALARIN EŞ ZAMANLI ÇALIŞTIRILMASI (THREADING) ---
            Console.WriteLine("\n--- TUM ALGORITMALAR CALISTIRILIYOR (THREADING) ---\n");

            Thread t1 = new Thread(() => Run_FCFS(ListeKopyala(hamListe)));
            Thread t2 = new Thread(() => Run_Preemptive_Simulation(ListeKopyala(hamListe), 1)); // SJF Preemptive
            Thread t3 = new Thread(() => Run_RoundRobin(ListeKopyala(hamListe), 5)); // RR Quantum=5
            Thread t4 = new Thread(() => Run_NonPreemptive_Simulation(ListeKopyala(hamListe), 1)); // SJF Non-Preemptive
            Thread t5 = new Thread(() => Run_Preemptive_Simulation(ListeKopyala(hamListe), 2)); // Priority Preemptive
            Thread t6 = new Thread(() => Run_NonPreemptive_Simulation(ListeKopyala(hamListe), 2)); // Priority Non-Preemptive

            t1.Start(); t2.Start(); t3.Start(); t4.Start(); t5.Start(); t6.Start();
            t1.Join(); t2.Join(); t3.Join(); t4.Join(); t5.Join(); t6.Join();

            Console.WriteLine("\n\nBütün islemler tamamlandi. .txt dosyalarini kontrol et!");
        }

        // --- YARDIMCI FONKSİYONLAR ---

        static List<Process> CSVOku(string path)
        {
            var liste = new List<Process>();
            var satirlar = File.ReadAllLines(path);
            for (int i = 0; i < satirlar.Length; i++)
            {
                var parcalar = satirlar[i].Split(','); // Ayraca dikkat (virgül veya noktalı virgül)
                if (parcalar.Length >= 3)
                {
                    try {
                        var p = new Process();
                        p.ID = parcalar[0];
                        p.ArrivalTime = double.Parse(parcalar[1], CultureInfo.InvariantCulture);
                        p.BurstTime = double.Parse(parcalar[2], CultureInfo.InvariantCulture);
                        p.Priority = parcalar.Length > 3 ? int.Parse(parcalar[3]) : 0;
                        p.RemainingTime = p.BurstTime;
                        liste.Add(p);
                    } catch { }
                }
            }
            return liste;
        }

        static List<Process> ListeKopyala(List<Process> kaynak)
        {
            return kaynak.Select(p => p.Clone()).ToList();
        }

        // --- 1. FCFS ALGORİTMASI ---
        static void Run_FCFS(List<Process> processes)
        {
            double time = 0;
            processes = processes.OrderBy(p => p.ArrivalTime).ToList();

            foreach (var p in processes)
            {
                if (time < p.ArrivalTime) time = p.ArrivalTime;
                p.StartTime = time;
                time += p.BurstTime;
                
                p.CompletionTime = time;
                p.TurnaroundTime = p.CompletionTime - p.ArrivalTime;
                p.WaitingTime = p.TurnaroundTime - p.BurstTime;
            }
            // FCFS'de Context Switch 0 kabul edilir veya ihmal edilir
            SonuclariYazdir(processes, "FCFS", 0);
        }

        // --- 2. PREEMPTIVE (KESMELİ) MOTOR (SJF & PRIORITY) ---
        static void Run_Preemptive_Simulation(List<Process> tumSurecler, int type) 
        {
            double currentTime = 0;
            double contextSwitchCost = 0.001;
            int contextSwitchCount = 0;

            List<Process> readyQueue = new List<Process>();
            List<Process> finishedQueue = new List<Process>();
            Process currentProcess = null;

            int totalProcess = tumSurecler.Count;
            string algoName = (type == 1) ? "SJF Preemptive" : "Priority Preemptive";

            while (finishedQueue.Count < totalProcess)
            {
                var yeniGelenler = tumSurecler.Where(p => p.ArrivalTime <= currentTime).ToList();
                foreach (var p in yeniGelenler)
                {
                    readyQueue.Add(p);
                    tumSurecler.Remove(p);
                }

                Process selectedProcess = null;
                if (readyQueue.Count > 0)
                {
                    if (type == 1) // SJF
                        selectedProcess = readyQueue.OrderBy(p => p.RemainingTime).ThenBy(p => p.ArrivalTime).First();
                    else // Priority
                        selectedProcess = readyQueue.OrderBy(p => p.Priority).ThenBy(p => p.ArrivalTime).First();
                }

                if (selectedProcess != null)
                {
                    if (currentProcess != selectedProcess && currentProcess != null) 
                    {
                        currentTime += contextSwitchCost;
                        contextSwitchCount++;
                    }
                    currentProcess = selectedProcess;
                    if (currentProcess.StartTime == -1) currentProcess.StartTime = currentTime;

                    double timeStep = 1.0; 
                    if (currentProcess.RemainingTime < timeStep) timeStep = currentProcess.RemainingTime;
                    
                    // Kesme kontrolü için hassas zaman ilerletme
                    if (tumSurecler.Count > 0)
                    {
                        double nextArrival = tumSurecler.Min(p => p.ArrivalTime);
                        if (nextArrival > currentTime && nextArrival < currentTime + timeStep)
                            timeStep = nextArrival - currentTime;
                    }

                    currentTime += timeStep;
                    currentProcess.RemainingTime -= timeStep;

                    if (currentProcess.RemainingTime <= 0.0001)
                    {
                        currentProcess.CompletionTime = currentTime;
                        currentProcess.TurnaroundTime = currentProcess.CompletionTime - currentProcess.ArrivalTime;
                        currentProcess.WaitingTime = currentProcess.TurnaroundTime - currentProcess.BurstTime;
                        finishedQueue.Add(currentProcess);
                        readyQueue.Remove(currentProcess);
                        currentProcess = null;
                    }
                }
                else
                {
                    currentTime++;
                    if (tumSurecler.Count > 0)
                    {
                        double nextTime = tumSurecler.Min(p => p.ArrivalTime);
                        if (nextTime > currentTime) currentTime = nextTime;
                    }
                }
            }
            SonuclariYazdir(finishedQueue, algoName, contextSwitchCount);
        }

        // --- 3. ROUND ROBIN ALGORİTMASI ---
        static void Run_RoundRobin(List<Process> tumSurecler, int quantum)
        {
            double currentTime = 0;
            double contextSwitchCost = 0.001; 
            int contextSwitchCount = 0;

            Queue<Process> readyQueue = new Queue<Process>();
            List<Process> finishedQueue = new List<Process>();
            
            tumSurecler = tumSurecler.OrderBy(p => p.ArrivalTime).ToList();
            int processIndex = 0;
            
            while(processIndex < tumSurecler.Count && tumSurecler[processIndex].ArrivalTime <= currentTime)
            {
                readyQueue.Enqueue(tumSurecler[processIndex]);
                processIndex++;
            }

            Process currentProcess = null;

            while (readyQueue.Count > 0 || processIndex < tumSurecler.Count)
            {
                if (readyQueue.Count == 0)
                {
                    currentTime = tumSurecler[processIndex].ArrivalTime;
                    while(processIndex < tumSurecler.Count && tumSurecler[processIndex].ArrivalTime <= currentTime)
                    {
                        readyQueue.Enqueue(tumSurecler[processIndex]);
                        processIndex++;
                    }
                }

                currentProcess = readyQueue.Dequeue();
                if (currentProcess.StartTime == -1) currentProcess.StartTime = currentTime;

                // Basit CS mantığı
                if (readyQueue.Count > 0 || processIndex < tumSurecler.Count)
                {
                     currentTime += contextSwitchCost;
                     contextSwitchCount++;
                }

                double timeToRun = Math.Min(currentProcess.RemainingTime, quantum);
                currentTime += timeToRun;
                currentProcess.RemainingTime -= timeToRun;

                while(processIndex < tumSurecler.Count && tumSurecler[processIndex].ArrivalTime <= currentTime)
                {
                    readyQueue.Enqueue(tumSurecler[processIndex]);
                    processIndex++;
                }

                if (currentProcess.RemainingTime <= 0.0001)
                {
                    currentProcess.CompletionTime = currentTime;
                    currentProcess.TurnaroundTime = currentProcess.CompletionTime - currentProcess.ArrivalTime;
                    currentProcess.WaitingTime = currentProcess.TurnaroundTime - currentProcess.BurstTime;
                    finishedQueue.Add(currentProcess);
                }
                else
                {
                    readyQueue.Enqueue(currentProcess);
                }
            }
            SonuclariYazdir(finishedQueue, "Round Robin", contextSwitchCount);
        }

        // --- 4. NON-PREEMPTIVE (KESMESİZ) MOTOR ---
        static void Run_NonPreemptive_Simulation(List<Process> tumSurecler, int type)
        {
            string algoName = (type == 1) ? "SJF Non-Preemptive" : "Priority Non-Preemptive";
            double currentTime = 0;
            double contextSwitchCost = 0.001;
            int contextSwitchCount = 0;
            List<Process> readyQueue = new List<Process>();
            List<Process> finishedQueue = new List<Process>();
            string lastProcessId = ""; 

            while (finishedQueue.Count < tumSurecler.Count)
            {
                foreach (var p in tumSurecler)
                {
                    if (p.ArrivalTime <= currentTime && !readyQueue.Contains(p) && !finishedQueue.Contains(p))
                        readyQueue.Add(p);
                }

                if (readyQueue.Count > 0)
                {
                    Process selectedProcess = null;
                    if (type == 1) 
                        selectedProcess = readyQueue.OrderBy(p => p.BurstTime).ThenBy(p => p.ArrivalTime).First();
                    else 
                        selectedProcess = readyQueue.OrderBy(p => p.Priority).ThenBy(p => p.ArrivalTime).First();

                    if (lastProcessId != "" && lastProcessId != selectedProcess.ID)
                    {
                        currentTime += contextSwitchCost;
                        contextSwitchCount++;
                    }
                    lastProcessId = selectedProcess.ID;

                    if (selectedProcess.StartTime == -1) selectedProcess.StartTime = currentTime;
                    currentTime += selectedProcess.BurstTime;
                    
                    selectedProcess.CompletionTime = currentTime;
                    selectedProcess.TurnaroundTime = selectedProcess.CompletionTime - selectedProcess.ArrivalTime;
                    selectedProcess.WaitingTime = selectedProcess.TurnaroundTime - selectedProcess.BurstTime;

                    finishedQueue.Add(selectedProcess);
                    readyQueue.Remove(selectedProcess);
                }
                else
                {
                    var gelmemisler = tumSurecler.Where(p => !finishedQueue.Contains(p)).ToList();
                    if (gelmemisler.Count > 0) currentTime = gelmemisler.Min(p => p.ArrivalTime);
                }
            }
            SonuclariYazdir(finishedQueue, algoName, contextSwitchCount);
        }

        // --- SONUÇ YAZDIRMA VE RAPORLAMA ---
        static void SonuclariYazdir(List<Process> bitenler, string algoritmaAdi, int csSayisi)
        {
            // Lock mekanizması (Threading sırasında dosya çakışmasın diye)
            lock(Console.Out) 
            {
                double avgWait = bitenler.Count > 0 ? bitenler.Average(p => p.WaitingTime) : 0;
                double avgTurnaround = bitenler.Count > 0 ? bitenler.Average(p => p.TurnaroundTime) : 0;
                double maxWait = bitenler.Count > 0 ? bitenler.Max(p => p.WaitingTime) : 0;
                double maxTurnaround = bitenler.Count > 0 ? bitenler.Max(p => p.TurnaroundTime) : 0;
                
                int tp50 = bitenler.Count(p => p.CompletionTime <= 50);
                int tp100 = bitenler.Count(p => p.CompletionTime <= 100);
                int tp150 = bitenler.Count(p => p.CompletionTime <= 150);
                int tp200 = bitenler.Count(p => p.CompletionTime <= 200);

                double toplamBurst = bitenler.Sum(p => p.BurstTime);
                double sonBitisZamani = bitenler.Count > 0 ? bitenler.Max(p => p.CompletionTime) : 1;
                double verimlilik = toplamBurst / (sonBitisZamani + (csSayisi * 0.001)); 
                if (verimlilik > 1.0) verimlilik = 1.0;

                string dosyaAdi = algoritmaAdi.Replace(" ", "_") + ".txt";
                
                // Dosyaya Yaz
                try {
                    using (StreamWriter sw = new StreamWriter(dosyaAdi))
                    {
                        sw.WriteLine($"ALGORITMA: {algoritmaAdi}");
                        sw.WriteLine("--------------------------------------------------");
                        sw.WriteLine("a) Zaman Tablosu");
                        sw.WriteLine("Process\tBaslangic\tBitis\tBurst");
                        foreach (var p in bitenler.OrderBy(x => x.CompletionTime))
                        {
                            sw.WriteLine($"[{p.ID}]\t{p.StartTime:F1}\t\t{p.CompletionTime:F1}\t{p.BurstTime}");
                        }
                        sw.WriteLine("--------------------------------------------------");
                        sw.WriteLine($"b) Bekleme Suresi (WT) -> Ort: {avgWait:F4}, Max: {maxWait:F4}");
                        sw.WriteLine($"c) Turnaround Time (TAT) -> Ort: {avgTurnaround:F4}, Max: {maxTurnaround:F4}");
                        sw.WriteLine($"d) Throughput -> T=50:{tp50}, T=100:{tp100}, T=150:{tp150}, T=200:{tp200}");
                        sw.WriteLine($"e) CPU Verimliligi: %{verimlilik*100:F2}");
                        sw.WriteLine($"f) Toplam Baglam Degistirme (CS): {csSayisi}");
                    }
                    Console.WriteLine($"{algoritmaAdi} tamamlandi -> {dosyaAdi}");
                } catch {
                    Console.WriteLine($"HATA: {dosyaAdi} dosyasina yazilamadi!");
                }
            }
        }
    }
}