(() => {
    let trafficChart;

    const ensureChartJs = () => {
        if (!window.Chart) {
            throw new Error("Chart.js is required before monitoringCharts.js.");
        }
    };

    const createGradient = (chart) => {
        const { ctx, chartArea } = chart;

        if (!chartArea) {
            return "rgba(47, 128, 237, 0.16)";
        }

        const gradient = ctx.createLinearGradient(0, chartArea.top, 0, chartArea.bottom);
        gradient.addColorStop(0, "rgba(47, 128, 237, 0.32)");
        gradient.addColorStop(1, "rgba(47, 128, 237, 0.02)");
        return gradient;
    };

    window.myProxyMonitoring = {
        renderTrafficChart(canvas, data) {
            ensureChartJs();

            if (!canvas) {
                return;
            }

            if (trafficChart) {
                trafficChart.data.labels = data.labels;
                trafficChart.data.datasets[0].data = data.values;
                trafficChart.update();
                return;
            }

            trafficChart = new Chart(canvas, {
                type: "line",
                data: {
                    labels: data.labels,
                    datasets: [
                        {
                            label: "Yêu cầu",
                            data: data.values,
                            borderColor: "#2f80ed",
                            backgroundColor: (context) => createGradient(context.chart),
                            borderWidth: 3,
                            fill: true,
                            pointBackgroundColor: "#ffffff",
                            pointBorderColor: "#2f80ed",
                            pointBorderWidth: 2,
                            pointHoverRadius: 5,
                            pointRadius: 3,
                            tension: 0.36
                        }
                    ]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    interaction: {
                        intersect: false,
                        mode: "index"
                    },
                    plugins: {
                        legend: {
                            display: false
                        },
                        tooltip: {
                            backgroundColor: "#10192d",
                            displayColors: false,
                            padding: 12,
                            titleColor: "#ffffff",
                            bodyColor: "#d8e2f7",
                            callbacks: {
                                label: (context) => `${context.parsed.y} yêu cầu`
                            }
                        }
                    },
                    scales: {
                        x: {
                            border: {
                                display: false
                            },
                            grid: {
                                color: "rgba(96, 112, 143, 0.12)",
                                drawTicks: false
                            },
                            ticks: {
                                color: "#60708f",
                                maxRotation: 0,
                                autoSkipPadding: 18
                            }
                        },
                        y: {
                            beginAtZero: true,
                            border: {
                                display: false
                            },
                            grid: {
                                color: "rgba(96, 112, 143, 0.12)"
                            },
                            ticks: {
                                color: "#60708f",
                                precision: 0
                            }
                        }
                    }
                }
            });
        },
        destroyTrafficChart() {
            if (!trafficChart) {
                return;
            }

            trafficChart.destroy();
            trafficChart = undefined;
        }
    };
})();
