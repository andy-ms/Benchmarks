FROM microsoft/dotnet:2.1-sdk

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
# dotnet dependencies?
        libunwind-dev \
# wrk dependencies
        build-essential \
        libssl-dev \
# KoreBuild dependencies
        unzip \
        wget \
# BenchmarksServer dependencies
        git \
        procps \
        cgroup-bin \
# Perfview dependencies
        zip \
        linux-tools \
        lttng-tools \
        lttng-modules-dkms \
        liblttng-ust-dev \
        binutils \
# make dependencies
        flex \
        bison \
# nhttp2 dependencies
        g++ make binutils autoconf automake autotools-dev libtool pkg-config \
        zlib1g-dev libcunit1-dev libssl-dev libxml2-dev libev-dev libevent-dev libjansson-dev \
        libc-ares-dev libjemalloc-dev cython python3-dev python-setuptools libjemalloc-dev \
        libspdylay-dev \
    && rm -rf /var/lib/apt/lists/*

# Make perf visible for perfcollect
RUN cp /usr/bin/perf_4.9 /usr/bin/perf

# downloading perfcollect
#ADD https://raw.githubusercontent.com/dotnet/corefx-tools/master/src/performance/perfcollect/perfcollect /usr/bin/perfcollect
ADD https://gist.githubusercontent.com/sebastienros/6c28f099d579e37df4000fe457f277d6/raw/b3a585225053a2009ec4c7a8aff68d2c3359c61d/perfcollect /usr/bin/perfcollect
RUN chmod +x /usr/bin/perfcollect

# Build and install h2load
ENV NGHTTP2_VERSION 1.32.0
RUN cd /tmp \
    && wget -qO- "https://github.com/tatsuhiro-t/nghttp2/releases/download/v${NGHTTP2_VERSION}/nghttp2-${NGHTTP2_VERSION}.tar.gz" | tar -xz \
    && cd /tmp/nghttp2-$NGHTTP2_VERSION \
    && autoreconf -i \
    && automake \
    && autoconf \
    && ./configure --enable-app \
    && make \
    && make install \
    && rm -rf nghttp2-${NGHTTP2_VERSION}.tar.gz /tmp/nghttp2-$NGHTTP2_VERSION

# Build and install wrk
ENV WRK_VERSION 4.1.0
RUN cd /tmp \
    && wget https://github.com/wg/wrk/archive/$WRK_VERSION.tar.gz -O wrk.tar.gz \
    && tar xvzf wrk.tar.gz \
    && cd wrk-$WRK_VERSION \
    && make \
    && cp wrk /usr/local/bin \
    && cd .. \
    && rm -rf wrk.tar.gz wrk-$WRK_VERSION

# Install docker client
ENV DOCKER_VERSION 17.09.0-ce
RUN cd /tmp \
    && wget https://download.docker.com/linux/static/stable/x86_64/docker-$DOCKER_VERSION.tgz -O docker.tgz \
    && tar xvzf docker.tgz \
    && cp docker/docker /usr/bin \
    && rm -rf docker.tgz docker

WORKDIR /benchmarks

ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE true

COPY . .
