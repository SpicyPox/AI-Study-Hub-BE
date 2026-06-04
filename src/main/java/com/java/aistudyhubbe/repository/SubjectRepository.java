package com.java.aistudyhubbe.repository;

import com.java.aistudyhubbe.entity.Subject;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.UUID;

@Repository
public interface SubjectRepository extends JpaRepository<Subject, UUID> {
}
