import os
import glob
import pandas as pd
import numpy as np
import matplotlib.pyplot as plt

# Порядок и подписи агентов: три стратегии выбора «1 из 3» и два режима казино
AGENT_ORDER = ['random', 'priority', 'synergy', 'casino', 'casino_bettor']
AGENT_COLORS = {
    'random': '#e74c3c',
    'priority': '#3498db',
    'synergy': '#2ecc71',
    'casino': '#e67e22',
    'casino_bettor': '#8e44ad',
}
AGENT_LABELS = {
    'random': 'Случайный агент (Random)',
    'priority': 'Приоритетный агент (Priority)',
    'synergy': 'Синергетический агент (Synergy)',
    'casino': 'Казино без ставок (Casino)',
    'casino_bettor': 'Казино со ставками (Casino bettor)',
}
AGENT_SHORT = {
    'random': 'rnd', 'priority': 'pri', 'synergy': 'syn', 'casino': 'cas', 'casino_bettor': 'bet'
}
CONFIG_ORDER = ['uniform', 'Neutral', 'Moderate', 'Strong']
CONFIG_COLORS = {'uniform': '#7f8c8d', 'Neutral': '#3498db', 'Moderate': '#2ecc71', 'Strong': '#e67e22'}

ABLATION_ORDER = ['uniform', 'no_w_base', 'no_k_stack', 'no_k_syn', 'no_k_role', 'no_pity', 'no_lock', 'full']
ABLATION_LABELS = {
    'uniform': 'Равновероятный',
    'no_w_base': 'без w_base',
    'no_k_stack': 'без k_stack',
    'no_k_syn': 'без k_syn',
    'no_k_role': 'без k_role',
    'no_pity': 'без гарантии',
    'no_lock': 'без правила сокетов',
    'full': 'Полный алгоритм',
}


def ordered(values, order):
    present = [v for v in order if v in set(values)]
    extra = [v for v in values if v not in order]
    return present + sorted(extra)


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
    if not runs_files or not summary_files or not freq_files:
        return None, None, None

    # Самая свежая серия: все три файла одной выгрузки имеют общий штамп времени
    runs_files.sort(key=os.path.getmtime, reverse=True)
    stamp = os.path.basename(runs_files[0])[len("runs_"):-4]
    summary = os.path.join(output_dir, f"summary_{stamp}.csv")
    freq = os.path.join(output_dir, f"frequency_{stamp}.csv")
    if not os.path.exists(summary) or not os.path.exists(freq):
        summary_files.sort(key=os.path.getmtime, reverse=True)
        freq_files.sort(key=os.path.getmtime, reverse=True)
        summary, freq = summary_files[0], freq_files[0]
    return runs_files[0], summary, freq


def save(fig, out_path):
    plt.tight_layout()
    plt.savefig(out_path)
    plt.close(fig)
    print(f"[Chart] Saved: {out_path}")


def plot_distance_distribution(df_runs, out_path):
    fig, ax = plt.subplots(figsize=(9, 5))
    for agent in ordered(df_runs['agent'].unique(), AGENT_ORDER):
        sub = df_runs[df_runs['agent'] == agent]['distance']
        color = AGENT_COLORS.get(agent, '#95a5a6')
        label = AGENT_LABELS.get(agent, agent)
        ax.hist(sub, bins=35, alpha=0.35, color=color, density=True, label=label, edgecolor=color)
        density, bins = np.histogram(sub, bins=35, density=True)
        bin_centers = 0.5 * (bins[:-1] + bins[1:])
        ax.plot(bin_centers, density, color=color, lw=2.2)

    ax.set_title('Плотность распределения пройденной дистанции по стратегиям агентов')
    ax.set_xlabel('Дистанция заезда, метры')
    ax.set_ylabel('Плотность вероятности')
    ax.set_xlim(0, 4200)
    ax.axvline(1000, color='gray', linestyle='--', alpha=0.6, label='Казино 1 (1000 м)')
    ax.axvline(2000, color='gray', linestyle=':', alpha=0.6, label='Казино 2 (2000 м)')
    ax.axvline(3000, color='gray', linestyle='-.', alpha=0.6, label='Казино 3 (3000 м)')
    ax.legend(frameon=True, loc='upper right')
    save(fig, out_path)


def plot_entropy_vs_synergy(df_summary, out_path):
    fig, ax = plt.subplots(figsize=(8.5, 5.2))
    for cfg in ordered(df_summary['configuration'].unique(), CONFIG_ORDER):
        sub = df_summary[df_summary['configuration'] == cfg]
        color = CONFIG_COLORS.get(cfg, '#9b59b6')
        ax.scatter(sub['normalized_modifier_entropy'], sub['synergy_rate'] * 100,
                   s=140, color=color, label=f"Конфигурация: {cfg}", edgecolors='black', linewidth=1.2, zorder=5)
        for _, row in sub.iterrows():
            ax.annotate(AGENT_SHORT.get(row['agent'], row['agent']),
                        (row['normalized_modifier_entropy'], row['synergy_rate'] * 100),
                        textcoords='offset points', xytext=(6, 4), fontsize=8, color='#333333')

    ax.set_title('Компромисс: нормированная энтропия Шеннона vs доля собранных синергий')
    ax.set_xlabel('Нормированная информационная энтропия H_norm (разнообразие билдов)')
    ax.set_ylabel('Доля заездов с собранными синергиями, %')
    ax.legend(frameon=True, loc='lower left')
    save(fig, out_path)


def plot_modifier_frequency(df_freq, out_path):
    fig, ax = plt.subplots(figsize=(9, 6))
    avg_share = df_freq.groupby('modifier')['share'].mean().sort_values(ascending=True) * 100
    y_pos = np.arange(len(avg_share))
    colors = ['#2980b9' if x < 12 else '#e74c3c' for x in avg_share.values]
    bars = ax.barh(y_pos, avg_share.values, color=colors, height=0.65, edgecolor='black', linewidth=0.8)
    ax.set_yticks(y_pos)
    ax.set_yticklabels(avg_share.index)
    ax.set_xlabel('Средняя доля заездов, в которых взят модификатор, %')
    ax.set_title('Частота выбора модификаторов (проверка на доминирующую мета-стратегию)')
    ideal_share = 100.0 / len(avg_share)
    ax.axvline(ideal_share, color='#27ae60', linestyle='--', lw=1.8, label=f'Равновероятный выбор ({ideal_share:.1f}%)')
    for bar in bars:
        width = bar.get_width()
        ax.text(width + 0.3, bar.get_y() + bar.get_height() / 2, f'{width:.1f}%',
                va='center', ha='left', fontsize=9, fontweight='bold')
    ax.set_xlim(0, max(avg_share.values) + 6)
    ax.legend(frameon=True, loc='lower right')
    save(fig, out_path)


def plot_survival_curves(df_runs, out_path):
    fig, ax = plt.subplots(figsize=(9, 5.2))
    distances_grid = np.linspace(0, 4200, 200)
    for agent in ordered(df_runs['agent'].unique(), AGENT_ORDER):
        sub = df_runs[df_runs['agent'] == agent]['distance'].values
        n = len(sub)
        survival = [np.sum(sub >= d) / n * 100.0 for d in distances_grid]
        ax.plot(distances_grid, survival, color=AGENT_COLORS.get(agent, '#95a5a6'), lw=2.5,
                label=AGENT_LABELS.get(agent, agent))

    ax.set_title('Эмпирическая функция выживаемости Каплана-Мейера S(d)')
    ax.set_xlabel('Дистанция заезда d, метры')
    ax.set_ylabel('Вероятность преодоления дистанции S(d), %')
    ax.set_xlim(0, 4200)
    ax.set_ylim(-2, 105)
    for x, name, ls in ((1000, 'Казино 1', '--'), (2000, 'Казино 2', ':'), (3000, 'Казино 3', '-.')):
        ax.axvline(x, color='gray', linestyle=ls, alpha=0.5)
        ax.text(x + 20, 15, name, rotation=90, color='gray', fontsize=9)
    ax.axvline(4000, color='#c0392b', linestyle='-', alpha=0.6)
    ax.text(3950, 60, 'Финальный босс', rotation=90, color='#c0392b', fontsize=9, fontweight='bold')
    ax.legend(frameon=True, loc='upper right')
    save(fig, out_path)


def plot_boxplot_distances(df_runs, out_path):
    fig, ax = plt.subplots(figsize=(11, 5.2))
    configs = ordered(df_runs['configuration'].unique(), CONFIG_ORDER)
    agents = ordered(df_runs['agent'].unique(), AGENT_ORDER)

    data_to_plot, labels, colors = [], [], []
    for cfg in configs:
        for ag in agents:
            sub = df_runs[(df_runs['configuration'] == cfg) & (df_runs['agent'] == ag)]['distance']
            if len(sub) > 0:
                data_to_plot.append(sub.values)
                labels.append(f"{cfg[:4]}-{AGENT_SHORT.get(ag, ag[:3])}")
                colors.append(AGENT_COLORS.get(ag, '#95a5a6'))

    try:
        bp = ax.boxplot(data_to_plot, patch_artist=True, tick_labels=labels,
                        medianprops=dict(color='black', linewidth=1.5),
                        flierprops=dict(marker='o', markersize=3, alpha=0.3))
    except TypeError:
        bp = ax.boxplot(data_to_plot, patch_artist=True, labels=labels,
                        medianprops=dict(color='black', linewidth=1.5),
                        flierprops=dict(marker='o', markersize=3, alpha=0.3))

    for box, color in zip(bp['boxes'], colors):
        box.set_facecolor(color)
        box.set_alpha(0.7)

    ax.set_title('Диаграмма размаха (Box-Plot) финальных дистанций заезда')
    ax.set_ylabel('Дистанция, метры')
    ax.set_xlabel('Конфигурация алгоритма и агент (rnd, pri, syn — выбор 1 из 3; cas, bet — казино)')
    plt.xticks(rotation=60)
    save(fig, out_path)


def grouped_bars(ax, df, value_col, configs, agents, scale=1.0, fmt='{:.0f}'):
    width = 0.8 / max(1, len(agents))
    x = np.arange(len(configs))
    for i, ag in enumerate(agents):
        vals = []
        for cfg in configs:
            row = df[(df['configuration'] == cfg) & (df['agent'] == ag)]
            vals.append(float(row[value_col].iloc[0]) * scale if len(row) else np.nan)
        bars = ax.bar(x + (i - (len(agents) - 1) / 2) * width, vals, width,
                      color=AGENT_COLORS.get(ag, '#95a5a6'), edgecolor='black', linewidth=0.6,
                      label=AGENT_LABELS.get(ag, ag))
        for b, v in zip(bars, vals):
            if not np.isnan(v):
                ax.text(b.get_x() + b.get_width() / 2, b.get_height(), fmt.format(v),
                        ha='center', va='bottom', fontsize=7)
    ax.set_xticks(x)
    ax.set_xticklabels(configs)


def plot_first_synergy(df_summary, out_path):
    """Скорость сборки билда: сколько выборов и метров нужно до первой синергии."""
    configs = ordered(df_summary['configuration'].unique(), CONFIG_ORDER)
    agents = ordered(df_summary['agent'].unique(), AGENT_ORDER)
    fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(12, 5))

    grouped_bars(ax1, df_summary, 'mean_picks_to_first_synergy', configs, agents, fmt='{:.1f}')
    ax1.set_title('Средний номер выбора, замкнувшего первую синергию')
    ax1.set_ylabel('Выборов до первой синергии (меньше — лучше)')

    grouped_bars(ax2, df_summary, 'median_distance_to_first_synergy', configs, agents, fmt='{:.0f}')
    ax2.set_title('Медианная дистанция первой синергии')
    ax2.set_ylabel('Метров до первой синергии (меньше — лучше)')
    ax2.legend(frameon=True, loc='upper right', fontsize=8)
    save(fig, out_path)


def plot_casino_economy(df_summary, out_path):
    """Экономика жетонов: сколько потрачено, сколько пропало, как часто срабатывала гарантия."""
    df = df_summary[df_summary['agent'].isin(['casino', 'casino_bettor'])]
    if df.empty:
        print('[Chart] Нет данных по агентам казино — экономика жетонов пропущена')
        return
    configs = ordered(df['configuration'].unique(), CONFIG_ORDER)
    agents = ordered(df['agent'].unique(), AGENT_ORDER)
    fig, axes = plt.subplots(1, 3, figsize=(14, 4.6))

    grouped_bars(axes[0], df, 'mean_tokens_spent', configs, agents, fmt='{:.1f}')
    axes[0].set_title('Жетонов потрачено за заезд')
    grouped_bars(axes[1], df, 'mean_tokens_wasted', configs, agents, fmt='{:.1f}')
    axes[1].set_title('Жетонов пропало (после последнего казино)')
    grouped_bars(axes[2], df, 'pity_trigger_rate', configs, agents, scale=100, fmt='{:.0f}%')
    axes[2].set_title('Доля заездов со сработавшей гарантией, %')
    axes[2].legend(frameon=True, loc='upper left', fontsize=8)
    save(fig, out_path)


def plot_ablation(df_summary, out_path):
    """Абляция: вклад каждого элемента алгоритма относительно полной конфигурации."""
    configs = ordered(df_summary['configuration'].unique(), ABLATION_ORDER)
    agents = ordered(df_summary['agent'].unique(), AGENT_ORDER)
    fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(13, 5.2))

    labels = [ABLATION_LABELS.get(c, c) for c in configs]
    width = 0.8 / max(1, len(agents))
    x = np.arange(len(configs))

    for ax, col, title, scale in ((ax1, 'synergy_rate', 'Доля заездов с синергией, %', 100),
                                  (ax2, 'median_distance', 'Медианная дистанция, м', 1)):
        for i, ag in enumerate(agents):
            vals = []
            for cfg in configs:
                row = df_summary[(df_summary['configuration'] == cfg) & (df_summary['agent'] == ag)]
                vals.append(float(row[col].iloc[0]) * scale if len(row) else np.nan)
            ax.bar(x + (i - (len(agents) - 1) / 2) * width, vals, width,
                   color=AGENT_COLORS.get(ag, '#95a5a6'), edgecolor='black', linewidth=0.6,
                   label=AGENT_LABELS.get(ag, ag))
        # Опорная линия полной конфигурации для первого агента казино
        ref_agent = 'casino' if 'casino' in agents else agents[0]
        ref = df_summary[(df_summary['configuration'] == 'full') & (df_summary['agent'] == ref_agent)]
        if len(ref):
            ax.axhline(float(ref[col].iloc[0]) * scale, color='#2c3e50', linestyle='--', lw=1.2,
                       label=f'Полный алгоритм ({AGENT_SHORT.get(ref_agent, ref_agent)})')
        ax.set_xticks(x)
        ax.set_xticklabels(labels, rotation=30, ha='right')
        ax.set_title(title)

    ax1.set_ylabel('%')
    ax2.set_ylabel('метры')
    ax2.legend(frameon=True, loc='lower right', fontsize=8)
    fig.suptitle('Абляция алгоритма: отключение одного элемента взвешивания или правила пула')
    save(fig, out_path)


def main():
    set_academic_style()

    base_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    output_dir = os.path.join(base_dir, "Simulation", "Output")
    ablation_dir = os.path.join(output_dir, "Ablation")
    charts_dir = os.path.join(base_dir, "Artifacts", "Charts")
    os.makedirs(charts_dir, exist_ok=True)

    runs_file, summary_file, freq_file = find_latest_files(output_dir)
    if runs_file is None:
        raise SystemExit("Нет выгрузок в Simulation/Output — сначала запустите серию в Unity")
    print(f"[Data] Using runs dataset: {os.path.basename(runs_file)}")
    print(f"[Data] Using summary dataset: {os.path.basename(summary_file)}")
    print(f"[Data] Using frequency dataset: {os.path.basename(freq_file)}")

    df_runs = pd.read_csv(runs_file, sep=';')
    df_summary = pd.read_csv(summary_file, sep=';')
    df_freq = pd.read_csv(freq_file, sep=';')
    print(f"[Data] Total runs in sample: {len(df_runs)}; agents: {sorted(df_runs['agent'].unique())}")

    plot_distance_distribution(df_runs, os.path.join(charts_dir, "distance_distribution.png"))
    plot_entropy_vs_synergy(df_summary, os.path.join(charts_dir, "entropy_vs_synergies.png"))
    plot_modifier_frequency(df_freq, os.path.join(charts_dir, "modifier_frequency.png"))
    plot_survival_curves(df_runs, os.path.join(charts_dir, "survival_curves.png"))
    plot_boxplot_distances(df_runs, os.path.join(charts_dir, "boxplot_distances.png"))

    if 'mean_picks_to_first_synergy' in df_summary.columns:
        plot_first_synergy(df_summary, os.path.join(charts_dir, "first_synergy.png"))
        plot_casino_economy(df_summary, os.path.join(charts_dir, "casino_economy.png"))
    else:
        print('[Chart] В сводке нет новых метрик — перезапустите серию в Unity')

    _, ablation_summary, _ = find_latest_files(ablation_dir)
    if ablation_summary:
        print(f"[Data] Using ablation summary: {os.path.basename(ablation_summary)}")
        plot_ablation(pd.read_csv(ablation_summary, sep=';'), os.path.join(charts_dir, "ablation.png"))
    else:
        print('[Chart] Нет выгрузки абляции (Simulation/Output/Ablation) — график абляции пропущен')

    print("\n[Success] Charts generated in Artifacts/Charts/")


if __name__ == "__main__":
    main()
