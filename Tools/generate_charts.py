import os
import glob
import pandas as pd
import numpy as np
import matplotlib.pyplot as plt

def set_academic_style():
    plt.style.use('seaborn-v0_8-whitegrid' if 'seaborn-v0_8-whitegrid' in plt.style.available else 'default')
    plt.rcParams.update({
        'font.size': 11,
        'axes.labelsize': 12,
        'axes.titlesize': 13,
        'xtick.labelsize': 10,
        'ytick.labelsize': 10,
        'legend.fontsize': 10,
        'figure.titlesize': 14,
        'figure.dpi': 300,
        'savefig.dpi': 300,
        'savefig.bbox': 'tight'
    })

def find_latest_files(output_dir):
    runs_files = glob.glob(os.path.join(output_dir, "runs_*.csv"))
    summary_files = glob.glob(os.path.join(output_dir, "summary_*.csv"))
    freq_files = glob.glob(os.path.join(output_dir, "frequency_*.csv"))
    
    # Sort by file size/date to pick the largest/richest run batch
    runs_files.sort(key=os.path.getsize, reverse=True)
    summary_files.sort(key=os.path.getmtime, reverse=True)
    freq_files.sort(key=os.path.getmtime, reverse=True)

    return runs_files[0], summary_files[0], freq_files[0]

def plot_distance_distribution(df_runs, out_path):
    fig, ax = plt.subplots(figsize=(9, 5))
    agents = df_runs['agent'].unique()
    colors = {'random': '#e74c3c', 'priority': '#3498db', 'synergy': '#2ecc71'}
    labels = {'random': 'Случайный агент (Random)', 
              'priority': 'Приоритетный агент (Priority)', 
              'synergy': 'Синергетический агент (Synergy)'}

    for agent in agents:
        sub = df_runs[df_runs['agent'] == agent]['distance']
        color = colors.get(agent, '#95a5a6')
        label = labels.get(agent, agent)
        ax.hist(sub, bins=35, alpha=0.45, color=color, density=True, label=label, edgecolor=color)
        # Smooth line
        density, bins = np.histogram(sub, bins=35, density=True)
        bin_centers = 0.5 * (bins[:-1] + bins[1:])
        ax.plot(bin_centers, density, color=color, lw=2.2)

    ax.set_title('Плотность распределения пройденной дистанции по стратегиям агентов')
    ax.set_xlabel('Дистанция заезда, метры')
    ax.set_ylabel('Плотность вероятности')
    ax.set_xlim(0, 4200)
    ax.axvline(1000, color='gray', linestyle='--', alpha=0.6, label='Сектор 1 (1000 м)')
    ax.axvline(2000, color='gray', linestyle=':', alpha=0.6, label='Сектор 2 (2000 м)')
    ax.axvline(3000, color='gray', linestyle='-.', alpha=0.6, label='Сектор 3 (3000 м)')
    ax.legend(frameon=True, loc='upper right')
    plt.tight_layout()
    plt.savefig(out_path)
    plt.close()
    print(f"[Chart] Saved: {out_path}")

def plot_entropy_vs_synergy(df_summary, out_path):
    fig, ax = plt.subplots(figsize=(8.5, 5.2))
    
    # Map colors and markers
    configs = df_summary['configuration'].unique()
    palette = {'uniform': '#7f8c8d', 'Neutral': '#3498db', 'Moderate': '#2ecc71', 'Strong': '#e67e22'}
    
    for cfg in configs:
        sub = df_summary[df_summary['configuration'] == cfg]
        color = palette.get(cfg, '#9b59b6')
        ax.scatter(sub['normalized_modifier_entropy'], sub['synergy_rate'] * 100, 
                   s=140, color=color, label=f"Конфигурация: {cfg}", edgecolors='black', linewidth=1.2, zorder=5)

    # Highlight Pareto optimal trade-off zone
    ax.axvspan(0.965, 0.985, color='#2ecc71', alpha=0.15, label='Оптимальная зона баланса (Moderate)')
    
    ax.set_title('Компромисс: Нормированная энтропия Шеннона vs Доля собранных синергий')
    ax.set_xlabel('Нормированная информационная энтропия H_norm (Разнообразие билдов)')
    ax.set_ylabel('Доля заездов со собранными синергиями, %')
    ax.set_xlim(0.93, 1.005)
    ax.set_ylim(60, 103)
    ax.legend(frameon=True, loc='lower left')
    plt.tight_layout()
    plt.savefig(out_path)
    plt.close()
    print(f"[Chart] Saved: {out_path}")

def plot_modifier_frequency(df_freq, out_path):
    fig, ax = plt.subplots(figsize=(9, 6))
    
    # Aggregated share per modifier
    avg_share = df_freq.groupby('modifier')['share'].mean().sort_values(ascending=True) * 100
    
    y_pos = np.arange(len(avg_share))
    colors = ['#2980b9' if x < 12 else '#e74c3c' for x in avg_share.values]
    
    bars = ax.barh(y_pos, avg_share.values, color=colors, height=0.65, edgecolor='black', linewidth=0.8)
    ax.set_yticks(y_pos)
    ax.set_yticklabels(avg_share.index)
    ax.set_xlabel('Средняя доля выборов модификатора, %')
    ax.set_title('Частота выбора модификаторов в пуле драфта (Проверка на доминирующую мета-стратегию)')
    
    # Reference lines: equal distribution = 1/12 ≈ 8.33%, healthy ceiling = 15%
    ideal_share = 100.0 / len(avg_share)
    ax.axvline(ideal_share, color='#27ae60', linestyle='--', lw=1.8, label=f'Равновероятный выбор ({ideal_share:.1f}%)')
    ax.axvline(15.0, color='#c0392b', linestyle=':', lw=1.8, label='Порог доминирования (15.0%)')
    
    for bar in bars:
        width = bar.get_width()
        ax.text(width + 0.3, bar.get_y() + bar.get_height()/2, f'{width:.1f}%', 
                va='center', ha='left', fontsize=9, fontweight='bold')

    ax.set_xlim(0, max(avg_share.values) + 3.5)
    ax.legend(frameon=True, loc='lower right')
    plt.tight_layout()
    plt.savefig(out_path)
    plt.close()
    print(f"[Chart] Saved: {out_path}")

def plot_survival_curves(df_runs, out_path):
    fig, ax = plt.subplots(figsize=(9, 5.2))
    
    distances_grid = np.linspace(0, 4200, 200)
    agents = df_runs['agent'].unique()
    colors = {'random': '#e74c3c', 'priority': '#3498db', 'synergy': '#2ecc71'}
    labels = {'random': 'Случайный агент', 'priority': 'Приоритетный агент', 'synergy': 'Синергетический агент'}
    
    for agent in agents:
        sub = df_runs[df_runs['agent'] == agent]['distance'].values
        n = len(sub)
        # S(d) = count(dist >= d) / n
        survival = [np.sum(sub >= d) / n * 100.0 for d in distances_grid]
        color = colors.get(agent, '#95a5a6')
        label = labels.get(agent, agent)
        ax.plot(distances_grid, survival, color=color, lw=2.5, label=label)

    ax.set_title('Эмпирическая функция выживаемости Каплана-Мейера S(d)')
    ax.set_xlabel('Дистанция заезда d, метры')
    ax.set_ylabel('Вероятность преодоления дистанции S(d), %')
    ax.set_xlim(0, 4200)
    ax.set_ylim(-2, 105)
    
    # Markers for sectors
    ax.axvline(1000, color='gray', linestyle='--', alpha=0.5)
    ax.text(1020, 15, 'Сектор 1', rotation=90, color='gray', fontsize=9)
    ax.axvline(2000, color='gray', linestyle=':', alpha=0.5)
    ax.text(2020, 15, 'Сектор 2', rotation=90, color='gray', fontsize=9)
    ax.axvline(3000, color='gray', linestyle='-.', alpha=0.5)
    ax.text(3020, 15, 'Сектор 3', rotation=90, color='gray', fontsize=9)
    ax.axvline(4000, color='#c0392b', linestyle='-', alpha=0.6)
    ax.text(3950, 60, 'Финальный Босс', rotation=90, color='#c0392b', fontsize=9, fontweight='bold')

    ax.legend(frameon=True, loc='upper right')
    plt.tight_layout()
    plt.savefig(out_path)
    plt.close()
    print(f"[Chart] Saved: {out_path}")

def plot_boxplot_distances(df_runs, out_path):
    fig, ax = plt.subplots(figsize=(9.5, 5))
    
    data_to_plot = []
    labels = []
    palette = ['#e74c3c', '#3498db', '#2ecc71']
    
    configs = ['uniform', 'Neutral', 'Moderate', 'Strong']
    agents = ['random', 'priority', 'synergy']
    
    for cfg in configs:
        for ag in agents:
            sub = df_runs[(df_runs['configuration'] == cfg) & (df_runs['agent'] == ag)]['distance']
            if len(sub) > 0:
                data_to_plot.append(sub.values)
                labels.append(f"{cfg[:4]}-{ag[:3]}")

    try:
        bp = ax.boxplot(data_to_plot, patch_artist=True, tick_labels=labels,
                        medianprops=dict(color='black', linewidth=1.5),
                        flierprops=dict(marker='o', markersize=3, alpha=0.3))
    except TypeError:
        bp = ax.boxplot(data_to_plot, patch_artist=True, labels=labels,
                        medianprops=dict(color='black', linewidth=1.5),
                        flierprops=dict(marker='o', markersize=3, alpha=0.3))

    for i, box in enumerate(bp['boxes']):
        agent_idx = i % 3
        box.set_facecolor(palette[agent_idx])
        box.set_alpha(0.7)

    ax.set_title('Диаграмма размаха (Box-Plot) финальных дистанций заезда')
    ax.set_ylabel('Дистанция, метры')
    ax.set_xlabel('Конфигурация алгоритма и тип агента (rnd=Случайный, pri=Приоритетный, syn=Синергия)')
    plt.xticks(rotation=45)
    plt.tight_layout()
    plt.savefig(out_path)
    plt.close()
    print(f"[Chart] Saved: {out_path}")

def main():
    set_academic_style()
    
    base_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    output_dir = os.path.join(base_dir, "Simulation", "Output")
    charts_dir = os.path.join(base_dir, "Artifacts", "Charts")
    os.makedirs(charts_dir, exist_ok=True)
    
    runs_file, summary_file, freq_file = find_latest_files(output_dir)
    print(f"[Data] Using runs dataset: {os.path.basename(runs_file)}")
    print(f"[Data] Using summary dataset: {os.path.basename(summary_file)}")
    print(f"[Data] Using frequency dataset: {os.path.basename(freq_file)}")
    
    df_runs = pd.read_csv(runs_file, sep=';')
    df_summary = pd.read_csv(summary_file, sep=';')
    df_freq = pd.read_csv(freq_file, sep=';')
    
    print(f"[Data] Total runs in sample: {len(df_runs)}")
    
    # 1. Distribution of distances
    plot_distance_distribution(df_runs, os.path.join(charts_dir, "distance_distribution.png"))
    
    # 2. Entropy vs Synergies
    plot_entropy_vs_synergy(df_summary, os.path.join(charts_dir, "entropy_vs_synergies.png"))
    
    # 3. Modifier popularity / frequency
    plot_modifier_frequency(df_freq, os.path.join(charts_dir, "modifier_frequency.png"))
    
    # 4. Survival curves
    plot_survival_curves(df_runs, os.path.join(charts_dir, "survival_curves.png"))
    
    # 5. Boxplot
    plot_boxplot_distances(df_runs, os.path.join(charts_dir, "boxplot_distances.png"))
    
    print("\n[Success] All 5 scientific charts generated successfully in Artifacts/Charts/!")

if __name__ == "__main__":
    main()
