#!/bin/bash
#PBS -N 5-lr-speech-translation
#PBS -q gpu
#PBS -l walltime=24:00:00
#PBS -l select=1:ncpus=8:ngpus=1:mem=16gb:scratch_local=32gb:cluster=glados

marian_build_dir=/storage/brno3-cerit/home/kwan/marian-installation/marian/build-CUDA-10.1-CPU-sse2-ssse3-avx-avx2
this_run_dir=/storage/brno3-cerit/home/kwan/marian-training/final-runs/baseline/runs/5-lr
marian_models_dir=$this_run_dir/models
cs_input=$this_run_dir/transcribed-speech/comp_linguistics-puncnormalized.tokenized.truecased.bpe.cs
vi_output=$this_run_dir/comp_linguistics-translated-puncnormalized.tokenized.truecased.bpe.vi
vi_reference=$this_run_dir/transcribed-speech/comp_linguistics-puncnormalized.tokenized.truecased.bpe.vi

# Add cuda to run marian training.
module add cuda-10.1

$marian_build_dir/marian-decoder \
	-d 0 \
	-c "${marian_models_dir}/model.npz.decoder.yml" \
	-m "${marian_models_dir}/model.iter35000.npz" \
	< "$cs_input" > "$vi_output"
